using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemPenjadwalanShiftGA.Data;
using SistemPenjadwalanShiftGA.Interfaces;

namespace SistemPenjadwalanShiftGA.Controllers
{
    [AllowAnonymous]
    public class VerifyController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IPdfExportService _pdfExportService;
        private readonly IWebHostEnvironment _env;

        public VerifyController(
            AppDbContext context,
            IPdfExportService pdfExportService,
            IWebHostEnvironment env)
        {
            _context = context;
            _pdfExportService = pdfExportService;
            _env = env;
        }

        [AllowAnonymous]
        [HttpGet("Verify/{code}")]
        public async Task<IActionResult> Index(string code)
        {
            return await Pdf(code);
        }

        [AllowAnonymous]
        [HttpGet("Verify/Pdf/{code}")]
        public async Task<IActionResult> Pdf(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest("Kode verifikasi tidak valid.");
            }

            var period = await _context.SchedulePeriods
                .FirstOrDefaultAsync(p => p.QrVerificationCode == code);

            if (period == null)
            {
                return NotFound("Dokumen jadwal tidak ditemukan atau kode verifikasi tidak valid.");
            }

            var safeCode = string.IsNullOrWhiteSpace(period.ScheduleCode) ? $"SCH-{period.Id:D4}" : period.ScheduleCode;
            var fileName = $"Jadwal_{safeCode}.pdf";

            // 1. Periksa apakah dokumen PDF fisik sudah tersimpan di server
            if (!string.IsNullOrEmpty(period.PdfFilePath))
            {
                var relativePath = period.PdfFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var physicalPath = Path.Combine(_env.WebRootPath, relativePath);
                if (System.IO.File.Exists(physicalPath))
                {
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                    Response.Headers["Content-Disposition"] = $"inline; filename=\"{Path.GetFileName(physicalPath)}\"";
                    return File(fileBytes, "application/pdf");
                }
            }

            // 2. Jika file fisik belum ada atau hilang, buat secara otomatis (on-the-fly)
            var (pdfBytes, _) = await _pdfExportService.GenerateSchedulePdfAsync(period.Id, "Manager Operasional");

            try
            {
                var pdfFolder = Path.Combine(_env.WebRootPath, "penjadwalan_pdf");
                if (!Directory.Exists(pdfFolder))
                {
                    Directory.CreateDirectory(pdfFolder);
                }
                var savePath = Path.Combine(pdfFolder, fileName);
                await System.IO.File.WriteAllBytesAsync(savePath, pdfBytes);
                period.PdfFilePath = $"/penjadwalan_pdf/{fileName}";
                await _context.SaveChangesAsync();
            }
            catch { /* non-blocking */ }

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            return File(pdfBytes, "application/pdf");
        }
    }
}
