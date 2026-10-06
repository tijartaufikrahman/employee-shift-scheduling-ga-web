using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SistemPenjadwalanShiftGA.Services
{
    public class PdfExportService : IPdfExportService
    {
        private readonly IScheduleRepository _scheduleRepository;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PdfExportService(
            IScheduleRepository scheduleRepository,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor)
        {
            _scheduleRepository = scheduleRepository;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<(byte[] PdfBytes, string QrVerificationCode)> GenerateSchedulePdfAsync(int periodId, string managerName)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var period = await _scheduleRepository.GetPeriodByIdAsync(periodId);
            if (period == null)
            {
                throw new ArgumentException("Periode jadwal tidak ditemukan.");
            }

            var details = (await _scheduleRepository.GetDetailsByPeriodIdAsync(periodId)).ToList();

            // Ensure verification code exists
            var verificationCode = period.QrVerificationCode;
            if (string.IsNullOrEmpty(verificationCode))
            {
                verificationCode = Guid.NewGuid().ToString("N");
                period.QrVerificationCode = verificationCode;
                await _scheduleRepository.UpdatePeriodAsync(period);
                await _scheduleRepository.SaveChangesAsync();
            }

            // Deteksi Base URL Otomatis (Lokal & VPS tanpa perlu ubah setting apa pun):
            // 1. Cek konfigurasi eksplisit jika diisi
            var configuredBaseUrl = _configuration["App:BaseUrl"]?.TrimEnd('/');
            string baseUrl = string.Empty;

            if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                baseUrl = configuredBaseUrl;
            }
            else
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && httpContext.Request != null)
                {
                    var req = httpContext.Request;

                    // A. Ambil dari header Origin (selalu menyertakan protokol, IP/domain, dan PORT asli dari browser)
                    var origin = req.Headers["Origin"].ToString().TrimEnd('/');
                    if (!string.IsNullOrWhiteSpace(origin) && (origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        baseUrl = origin;
                    }

                    // B. Jika Origin kosong (misal GET ExportPdf biasa), ambil dari Referer browser
                    if (string.IsNullOrWhiteSpace(baseUrl))
                    {
                        var referer = req.Headers["Referer"].ToString();
                        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
                        {
                            baseUrl = refererUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
                        }
                    }

                    // C. Ambil dari Host request / Forwarded headers
                    if (string.IsNullOrWhiteSpace(baseUrl))
                    {
                        var host = req.Headers["X-Forwarded-Host"].ToString();
                        if (string.IsNullOrWhiteSpace(host))
                        {
                            host = req.Host.Value;
                        }

                        var scheme = req.Headers["X-Forwarded-Proto"].ToString();
                        if (string.IsNullOrWhiteSpace(scheme))
                        {
                            scheme = req.Scheme;
                        }

                        var port = req.Headers["X-Forwarded-Port"].ToString();
                        if (!string.IsNullOrWhiteSpace(port) && !host.Contains(':'))
                        {
                            if ((scheme == "http" && port != "80") || (scheme == "https" && port != "443"))
                            {
                                host = $"{host}:{port}";
                            }
                        }

                        baseUrl = $"{scheme}://{host}".TrimEnd('/');
                    }
                }

                // Fallback jika di luar request context
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    baseUrl = "https://localhost:9008";
                }
            }

            var qrUrl = $"{baseUrl}/Verify/Pdf/{verificationCode}";

            // Generate high-resolution QR Code
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20);

            var users = details
                .Where(d => d.User != null)
                .Select(d => d.User!)
                .DistinctBy(u => u.Id)
                .OrderBy(u => u.NamaLengkap)
                .ToList();

            var dates = details
                .Select(d => d.WorkDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (!dates.Any())
            {
                for (var d = period.StartDate.Date; d <= period.EndDate.Date; d = d.AddDays(1))
                {
                    dates.Add(d);
                }
            }

            int totalDays = dates.Count;
            string durationLabel = totalDays >= 14 ? "14 Hari" : $"{totalDays} Hari";
            string statusText = period.IsPublished ? "RESMI BERLAKU" : "DRAFT PENJADWALAN";
            string statusColor = period.IsPublished ? "#059669" : "#d97706";
            string scheduleCodeDisplay = string.IsNullOrWhiteSpace(period.ScheduleCode) ? $"SCH-{period.Id:D4}" : period.ScheduleCode;

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8.5f));

                    // ==========================================
                    // HEADER WITH TOP-RIGHT QR CODE
                    // ==========================================
                    page.Header().Element(header =>
                    {
                        header.PaddingBottom(10).Row(row =>
                        {
                            // Left Header Info
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("SISTEM INFORMASI PENJADWALAN KERJA OPERASIONAL")
                                    .FontSize(8.5f)
                                    .Bold()
                                    .FontColor("#0f766e");

                                col.Item().Text("JADWAL KERJA OPERASIONAL KARYAWAN")
                                    .FontSize(15)
                                    .Bold()
                                    .FontColor("#0f172a");

                                col.Item().PaddingTop(4).Table(metaTable =>
                                {
                                    metaTable.ColumnsDefinition(metaCols =>
                                    {
                                        metaCols.ConstantColumn(85);
                                        metaCols.ConstantColumn(8);
                                        metaCols.RelativeColumn();
                                    });

                                    // Row 1: Kode Penjadwalan
                                    metaTable.Cell().PaddingVertical(1).Text("Kode Penjadwalan").Bold().FontColor("#334155").FontSize(8);
                                    metaTable.Cell().PaddingVertical(1).Text(":").Bold().FontColor("#334155").FontSize(8);
                                    metaTable.Cell().PaddingVertical(1).Text(scheduleCodeDisplay).Bold().FontColor("#0f766e").FontSize(8);

                                    // Row 2: Periode Jadwal
                                    metaTable.Cell().PaddingVertical(1).Text("Periode Jadwal").Bold().FontColor("#334155").FontSize(8);
                                    metaTable.Cell().PaddingVertical(1).Text(":").Bold().FontColor("#334155").FontSize(8);
                                    metaTable.Cell().PaddingVertical(1).Text($"{period.StartDate:dd MMMM yyyy} s.d. {period.EndDate:dd MMMM yyyy} ({durationLabel})").FontColor("#475569").FontSize(8);

                                    // Row 3: Total Karyawan
                                    metaTable.Cell().PaddingVertical(1).Text("Total Karyawan").Bold().FontColor("#334155").FontSize(8);
                                    metaTable.Cell().PaddingVertical(1).Text(":").Bold().FontColor("#334155").FontSize(8);
                                    metaTable.Cell().PaddingVertical(1).Text($"{users.Count} Karyawan Terjadwal").FontColor("#475569").FontSize(8);

                                    // Row 4: Status Dokumen
                                    metaTable.Cell().PaddingVertical(1).Text("Status Dokumen").Bold().FontColor("#334155").FontSize(8);
                                    metaTable.Cell().PaddingVertical(1).Text(":").Bold().FontColor("#334155").FontSize(8);
                                    metaTable.Cell().PaddingVertical(1).Text(statusText).Bold().FontColor(statusColor).FontSize(8);

                                    // Row 5: Tanggal Disusun
                                    if (period.GeneratedAt.HasValue)
                                    {
                                        metaTable.Cell().PaddingVertical(1).Text("Tanggal Disusun").Bold().FontColor("#334155").FontSize(8);
                                        metaTable.Cell().PaddingVertical(1).Text(":").Bold().FontColor("#334155").FontSize(8);
                                        metaTable.Cell().PaddingVertical(1).Text($"{period.GeneratedAt.Value:dd MMMM yyyy HH:mm} WIB").FontColor("#475569").FontSize(8);
                                    }
                                });
                            });

                            // Right Header: QR Code Bersih (Tanpa Background Abu, Tanpa Border, Tanpa Teks)
                            row.ConstantItem(70).Column(qrCol =>
                            {
                                qrCol.Item().AlignRight().Width(65).Height(65).Image(qrCodeBytes);
                            });
                        });
                    });

                    // ==========================================
                    // CONTENT TABLE: GRID MATRIX SHIFT
                    // ==========================================
                    page.Content().Element(content =>
                    {
                        content.Column(col =>
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(130); // Employee Name
                                    foreach (var d in dates) cols.RelativeColumn();
                                    cols.ConstantColumn(38); // Total
                                    cols.ConstantColumn(38); // Nights
                                });

                                // Table Header
                                table.Header(th =>
                                {
                                    th.Cell().Background("#0f766e").Border(1).BorderColor("#0d9488").Padding(4)
                                        .Text("NAMA KARYAWAN").Bold().FontColor(Colors.White).FontSize(8);

                                    foreach (var date in dates)
                                    {
                                        var dayName = date.ToString("ddd", new System.Globalization.CultureInfo("id-ID")).ToUpper();
                                        var dateNum = date.ToString("dd/MM");

                                        th.Cell().Background("#0f766e").Border(1).BorderColor("#0d9488").Padding(3).AlignCenter()
                                            .Column(c =>
                                            {
                                                c.Item().AlignCenter().Text(dayName).Bold().FontColor("#5eead4").FontSize(7);
                                                c.Item().AlignCenter().Text(dateNum).Bold().FontColor(Colors.White).FontSize(7.5f);
                                            });
                                    }

                                    th.Cell().Background("#0f766e").Border(1).BorderColor("#0d9488").Padding(3).AlignCenter()
                                        .Text("TOTAL").Bold().FontColor(Colors.White).FontSize(7.5f);

                                    th.Cell().Background("#0f766e").Border(1).BorderColor("#0d9488").Padding(3).AlignCenter()
                                        .Text("MALAM").Bold().FontColor(Colors.White).FontSize(7.5f);
                                });

                                // Table Rows
                                int rowIndex = 0;
                                foreach (var user in users)
                                {
                                    rowIndex++;
                                    var rowBg = rowIndex % 2 == 0 ? "#f8fafc" : "#ffffff";
                                    int totalShifts = 0;
                                    int nightShifts = 0;

                                    // Employee Name Cell
                                    table.Cell().Background(rowBg).Border(1).BorderColor("#cbd5e1").Padding(4)
                                        .Row(r =>
                                        {
                                            r.RelativeItem().Text(user.NamaLengkap).Bold().FontColor("#0f172a");
                                            if (user.Level == "Senior")
                                            {
                                                r.ConstantItem(18).AlignRight().Text("Sr").FontSize(7).Bold().FontColor("#0f766e");
                                            }
                                        });

                                    // Shift Days Cells
                                    foreach (var date in dates)
                                    {
                                        var detail = details.FirstOrDefault(d => d.UserId == user.Id && d.WorkDate.Date == date);
                                        string text = "OFF";
                                        string cellBg = "#f8fafc";
                                        string textColor = "#94a3b8";

                                        if (detail != null)
                                        {
                                            if (detail.Status == "Sick")
                                            {
                                                text = "SICK";
                                                cellBg = "#fee2e2";
                                                textColor = "#b91c1c";
                                            }
                                            else if (detail.Status == "Replaced")
                                            {
                                                text = "REP";
                                                cellBg = "#fef3c7";
                                                textColor = "#92400e";
                                            }
                                            else if (detail.ShiftTypeId != null && detail.ShiftType != null)
                                            {
                                                 text = detail.ShiftType.ShiftCode;
                                                 totalShifts++;
                                                 if (detail.ShiftTypeId == period.NightShiftId || detail.ShiftType.ShiftCode.Equals("M", StringComparison.OrdinalIgnoreCase)) nightShifts++;

                                                if (text.Equals("P", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    cellBg = "#d1fae5";
                                                    textColor = "#065f46";
                                                }
                                                else if (text.Equals("S", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    cellBg = "#e0f2fe";
                                                    textColor = "#0369a1";
                                                }
                                                else if (text.Equals("M", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    cellBg = "#ede9fe";
                                                    textColor = "#6d28d9";
                                                }
                                            }
                                        }

                                        table.Cell().Background(cellBg).Border(1).BorderColor("#cbd5e1").Padding(3).AlignCenter()
                                            .Text(text).Bold().FontColor(textColor).FontSize(8);
                                    }

                                    // Total Cell
                                    table.Cell().Background("#f1f5f9").Border(1).BorderColor("#cbd5e1").Padding(3).AlignCenter()
                                        .Text(totalShifts.ToString()).Bold().FontColor("#0f172a").FontSize(8.5f);

                                    // Nights Cell
                                    table.Cell().Background("#f1f5f9").Border(1).BorderColor("#cbd5e1").Padding(3).AlignCenter()
                                        .Text(nightShifts.ToString()).Bold().FontColor("#7c3aed").FontSize(8.5f);
                                }
                            });

                            // Legend Bar Under Table
                            col.Item().PaddingTop(8).Row(legRow =>
                            {
                                legRow.RelativeItem().Text(t =>
                                {
                                    t.Span("KETERANGAN KODE SHIFT: ").Bold().FontColor("#334155").FontSize(7.5f);
                                    t.Span(" [P] Shift Pagi ").Bold().FontColor("#065f46").FontSize(7.5f);
                                    t.Span(" | ").FontColor("#94a3b8");
                                    t.Span(" [S] Shift Sore ").Bold().FontColor("#0369a1").FontSize(7.5f);
                                    t.Span(" | ").FontColor("#94a3b8");
                                    t.Span(" [M] Shift Malam ").Bold().FontColor("#6d28d9").FontSize(7.5f);
                                    t.Span(" | ").FontColor("#94a3b8");
                                    t.Span(" [OFF] Hari Libur ").FontColor("#64748b").FontSize(7.5f);
                                });
                            });
                        });
                    });

                    // ==========================================
                    // FOOTER & APPROVAL SIGNATURE BLOCK
                    // ==========================================
                    page.Footer().Element(f =>
                    {
                        f.PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"Dokumen ini diterbitkan secara resmi oleh Sistem Informasi Penjadwalan pada: {DateTime.Now:dd MMMM yyyy HH:mm} WIB")
                                    .FontSize(7.5f)
                                    .FontColor("#64748b");
                                c.Item().Text($"Tautan Verifikasi: {baseUrl}/Verify/{verificationCode}")
                                    .FontSize(7)
                                    .FontColor("#94a3b8");
                            });

                            row.ConstantItem(200).Column(c =>
                            {
                                c.Item().AlignRight().Text($"Disetujui secara digital oleh:")
                                    .FontSize(7.5f)
                                    .FontColor("#64748b");
                                c.Item().AlignRight().Text(managerName)
                                    .FontSize(9)
                                    .Bold()
                                    .FontColor("#0f172a");
                                c.Item().AlignRight().Text("Manager Operasional")
                                    .FontSize(7.5f)
                                    .FontColor("#0f766e");
                            });
                        });
                    });
                });
            });

            return (pdf.GeneratePdf(), verificationCode);
        }
    }
}
