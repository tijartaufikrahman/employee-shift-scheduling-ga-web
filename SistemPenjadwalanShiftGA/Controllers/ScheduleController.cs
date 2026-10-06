using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.ViewModels;
using SistemPenjadwalanShiftGA.Models.Entities;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace SistemPenjadwalanShiftGA.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class ScheduleController : Controller
    {
        private readonly IScheduleRepository _scheduleRepo;
        private readonly IScheduleService _scheduleService;
        private readonly IPdfExportService _pdfExportService;
        private readonly IShiftTypeRepository _shiftTypeRepo;
        private readonly IUserRepository _userRepo;
        private readonly ILeaveRequestRepository _leaveRepo;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public ScheduleController(
            IScheduleRepository scheduleRepo,
            IScheduleService scheduleService,
            IPdfExportService pdfExportService,
            IShiftTypeRepository shiftTypeRepo,
            IUserRepository userRepo,
            ILeaveRequestRepository leaveRepo,
            Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _scheduleRepo = scheduleRepo;
            _scheduleService = scheduleService;
            _pdfExportService = pdfExportService;
            _shiftTypeRepo = shiftTypeRepo;
            _userRepo = userRepo;
            _leaveRepo = leaveRepo;
            _cache = cache;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var allPeriods = (await _scheduleRepo.GetAllPeriodsAsync()).OrderByDescending(p => p.StartDate).ToList();
            var today = System.DateTime.UtcNow.AddHours(7).Date;

            var activePeriod = allPeriods.FirstOrDefault(p => p.IsPublished && p.StartDate.Date <= today && p.EndDate.Date >= today);
            if (activePeriod == null)
            {
                activePeriod = allPeriods.FirstOrDefault(p => p.IsPublished);
            }

            var draftPeriod = allPeriods.FirstOrDefault(p => !p.IsPublished);
            var recentCompletedPeriods = allPeriods.Where(p => p.IsPublished && p.EndDate.Date < today).Take(3).ToList();

            if (activePeriod != null)
            {
                var details = (await _scheduleRepo.GetDetailsByPeriodIdAsync(activePeriod.Id)).ToList();
                var totalStaff = details.Select(d => d.UserId).Distinct().Count();
                var todayDetails = details.Where(d => d.WorkDate.Date == today).ToList();

                ViewBag.ActiveTotalStaff = totalStaff;
                ViewBag.TodayMorningCount = todayDetails.Count(d => d.ShiftType != null && d.ShiftType.ShiftCode == "P");
                ViewBag.TodayAfternoonCount = todayDetails.Count(d => d.ShiftType != null && d.ShiftType.ShiftCode == "S");
                ViewBag.TodayNightCount = todayDetails.Count(d => d.ShiftType != null && (d.ShiftType.ShiftCode == "M" || d.ShiftType.ShiftName.ToLower().Contains("malam") || (activePeriod.NightShiftId != null && d.ShiftTypeId == activePeriod.NightShiftId)));
                ViewBag.TodayOffCount = todayDetails.Count(d => d.ShiftTypeId == null || d.ShiftType == null || d.Status == "Off");
            }

            ViewBag.ActivePeriod = activePeriod;
            ViewBag.DraftPeriod = draftPeriod;
            ViewBag.RecentCompletedPeriods = recentCompletedPeriods;
            ViewBag.TotalPeriodsCount = allPeriods.Count;
            ViewBag.ShiftTypes = (await _shiftTypeRepo.GetAllAsync()).OrderBy(s => s.StartTime).ToList();
            ViewBag.PrevSummary = await GetPreviousPeriodSummaryAsync();

            return View(allPeriods);
        }

        public async Task<IActionResult> History()
        {
            var periods = (await _scheduleRepo.GetAllPeriodsAsync())
                .OrderByDescending(p => p.StartDate)
                .ToList();
            ViewData["Title"] = "Riwayat Jadwal";
            return View(periods);
        }

        private async Task<PreviousPeriodSummaryDto?> GetPreviousPeriodSummaryAsync()
        {
            var allPeriods = (await _scheduleRepo.GetAllPeriodsAsync()).OrderByDescending(p => p.EndDate).ToList();
            var lastPeriod = allPeriods.FirstOrDefault(p => p.IsPublished) ?? allPeriods.FirstOrDefault();
            if (lastPeriod == null) return null;

            var details = (await _scheduleRepo.GetDetailsByPeriodIdAsync(lastPeriod.Id)).ToList();
            var lastDate = lastPeriod.EndDate.Date;
            var lastDayDetails = details.Where(d => d.WorkDate.Date == lastDate).ToList();

            var nightStaff = lastDayDetails
                .Where(d => d.ShiftTypeId != null && (d.ShiftTypeId == lastPeriod.NightShiftId || (d.ShiftType != null && (d.ShiftType.ShiftCode == "M" || d.ShiftType.ShiftName.ToLower().Contains("malam")))))
                .Select(d => d.User?.NamaLengkap ?? "Karyawan")
                .Distinct()
                .ToList();

            var morningStaff = lastDayDetails
                .Where(d => d.ShiftType != null && d.ShiftType.ShiftCode == "P")
                .Select(d => d.User?.NamaLengkap ?? "Karyawan")
                .Distinct()
                .ToList();

            var afternoonStaff = lastDayDetails
                .Where(d => d.ShiftType != null && d.ShiftType.ShiftCode == "S")
                .Select(d => d.User?.NamaLengkap ?? "Karyawan")
                .Distinct()
                .ToList();

            var totalStaff = details.Select(d => d.UserId).Distinct().Count();

            return new PreviousPeriodSummaryDto
            {
                Id = lastPeriod.Id,
                ScheduleCode = lastPeriod.ScheduleCode,
                PeriodName = lastPeriod.PeriodName,
                StartDate = lastPeriod.StartDate,
                EndDate = lastPeriod.EndDate,
                IsPublished = lastPeriod.IsPublished,
                TotalEmployees = totalStaff,
                LastDayNightStaff = nightStaff,
                LastDayMorningStaff = morningStaff,
                LastDayAfternoonStaff = afternoonStaff
            };
        }

        private async Task<List<EmployeeReadinessDto>> GetEmployeeReadinessAsync(DateTime startDate, DateTime endDate)
        {
            var activeUsers = (await _userRepo.GetAllAsync())
                .Where(u => u.RoleId == 2 && u.IsActive)
                .OrderBy(u => u.NamaLengkap)
                .ToList();

            var allLeaves = await _leaveRepo.GetAllAsync();
            var rangeLeaves = allLeaves
                .Where(l => l.RequestDate.Date >= startDate.Date && l.RequestDate.Date <= endDate.Date && l.Status != "Rejected")
                .ToList();

            var list = new List<EmployeeReadinessDto>();
            foreach (var u in activeUsers)
            {
                var userLeaves = rangeLeaves
                    .Where(l => l.UserId == u.Id)
                    .OrderBy(l => l.RequestDate)
                    .Select(l => new EmployeeLeaveItemDto
                    {
                        Id = l.Id,
                        RequestDate = l.RequestDate,
                        LeaveType = l.LeaveType,
                        Reason = l.Reason,
                        Status = l.Status
                    })
                    .ToList();

                list.Add(new EmployeeReadinessDto
                {
                    UserId = u.Id,
                    NamaLengkap = u.NamaLengkap,
                    Level = u.Level,
                    Leaves = userLeaves
                });
            }

            return list.OrderByDescending(e => e.Leaves.Any())
                       .ThenByDescending(e => e.Level == "Senior")
                       .ThenBy(e => e.NamaLengkap)
                       .ToList();
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeReadiness(DateTime startDate, int days = 14)
        {
            if (days <= 0) days = 14;
            var endDate = startDate.AddDays(days - 1);
            var readiness = await GetEmployeeReadinessAsync(startDate, endDate);

            var result = new
            {
                success = true,
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
                rangeFormatted = $"{startDate:dd MMM yyyy} – {endDate:dd MMM yyyy}",
                days = days,
                totalActive = readiness.Count,
                withLeave = readiness.Count(r => r.Leaves.Any()),
                fullyReady = readiness.Count(r => !r.Leaves.Any()),
                employees = readiness.Select(r => new
                {
                    userId = r.UserId,
                    namaLengkap = r.NamaLengkap,
                    level = r.Level,
                    hasLeave = r.Leaves.Any(),
                    hasApprovedLeave = r.HasApprovedLeave,
                    approvedLeaveType = r.ApprovedLeaveType,
                    hasPendingLeave = r.HasPendingLeave,
                    totalLeaveDays = r.TotalLeaveDays,
                    availabilityStatus = r.AvailabilityStatus,
                    leaves = r.Leaves.Select(l => new
                    {
                        id = l.Id,
                        requestDate = l.RequestDate.ToString("yyyy-MM-dd"),
                        formattedDate = l.FormattedDate,
                        leaveType = l.LeaveType,
                        reason = l.Reason,
                        status = l.Status
                    })
                })
            };

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> StaffCalculator()
        {
            ViewData["Title"] = "Kalkulator Kebutuhan Karyawan";
            var shifts = (await _shiftTypeRepo.GetAllAsync()).OrderBy(s => s.StartTime).ToList();
            return View(shifts);
        }

        public async Task<IActionResult> Generate()
        {
            var shifts = (await _shiftTypeRepo.GetAllAsync()).OrderBy(s => s.StartTime).ToList();
            ViewBag.ShiftTypes = shifts;
            var prevSummary = await GetPreviousPeriodSummaryAsync();

            DateTime suggestedDate = System.DateTime.UtcNow.AddHours(7).Date;
            if (prevSummary != null && prevSummary.EndDate >= System.DateTime.UtcNow.AddHours(7).Date.AddDays(-30))
            {
                suggestedDate = prevSummary.RecommendedNextStartDate;
            }

            var detectedNightShift = shifts.FirstOrDefault(s => s.ShiftCode.Equals("M", StringComparison.OrdinalIgnoreCase) || s.ShiftName.ToLower().Contains("malam"));

            int previewDays = 14; // Default 2 minggu preview
            DateTime previewEndDate = suggestedDate.AddDays(previewDays - 1);
            var readinessList = await GetEmployeeReadinessAsync(suggestedDate, previewEndDate);
            var allPeriods = (await _scheduleRepo.GetAllPeriodsAsync()).OrderByDescending(p => p.StartDate).ToList();

            var vm = new ScheduleGenerateViewModel
            {
                StartDate = suggestedDate,
                DurationType = "2_Weeks",
                MaxConsecutiveDays = 5,
                MaxOffDays = 1,
                PreventNightToMorning = true,
                MinSenior = 1,
                NightShiftId = detectedNightShift?.Id,
                PreviousPeriod = prevSummary,
                PreviousPeriodId = prevSummary?.Id,
                AvailableHistoryPeriods = allPeriods,
                EmployeeReadinessList = readinessList,
                PreviewStartDate = suggestedDate,
                PreviewEndDate = previewEndDate
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(ScheduleGenerateViewModel model)
        {
            int weeks = 2;
            int days = 14;
            if (model.DurationType == "1_Week")
            {
                weeks = 1;
                days = 7;
            }
            else if (model.DurationType.Contains("_Weeks"))
            {
                var digits = new string(model.DurationType.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out int w))
                {
                    weeks = Math.Max(1, w);
                    days = weeks * 7;
                }
            }

            if (!ModelState.IsValid)
            {
                model.PreviousPeriod = await GetPreviousPeriodSummaryAsync();
                model.AvailableHistoryPeriods = (await _scheduleRepo.GetAllPeriodsAsync()).OrderByDescending(p => p.StartDate).ToList();
                ViewBag.ShiftTypes = (await _shiftTypeRepo.GetAllAsync()).OrderBy(s => s.StartTime).ToList();
                model.PreviewStartDate = model.StartDate;
                model.PreviewEndDate = model.StartDate.AddDays(days - 1);
                model.EmployeeReadinessList = await GetEmployeeReadinessAsync(model.PreviewStartDate, model.PreviewEndDate);
                return View(model);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            // Auto-detect night shift if not specified
            if (!model.NightShiftId.HasValue || model.NightShiftId.Value <= 0)
            {
                var shifts = await _shiftTypeRepo.GetAllAsync();
                var detectedNightShift = shifts.FirstOrDefault(s => s.ShiftCode.Equals("M", StringComparison.OrdinalIgnoreCase) || s.ShiftName.ToLower().Contains("malam"));
                model.NightShiftId = detectedNightShift?.Id;
            }

            // model.MaxOffDays merepresentasikan Hari Libur Mingguan per Karyawan (default 1)
            // Logika proporsional: total hari libur periode = Hari Libur Mingguan x Jumlah Minggu
            int weeklyOffDays = model.MaxOffDays > 0 ? model.MaxOffDays : 1;
            int totalMaxOffDays = weeklyOffDays * weeks;

            var previewData = await _scheduleService.GenerateSchedulePreviewAsync(
                model.StartDate,
                model.DurationType,
                model.MaxConsecutiveDays,
                totalMaxOffDays,
                model.PreventNightToMorning,
                model.MinSenior,
                model.NightShiftId,
                userId,
                model.PreviousPeriodId);

            if (previewData != null && previewData.Details.Any())
            {
                var token = previewData.Token;
                _cache.Set($"SchedulePreview_{token}", previewData, System.TimeSpan.FromHours(2));
                return RedirectToAction(nameof(Preview), new { token = token });
            }

            ModelState.AddModelError("", "Gagal membuat pratinjau jadwal. Silakan periksa ketersediaan karyawan atau pengaturan kuota shift.");
            model.PreviousPeriod = await GetPreviousPeriodSummaryAsync();
            model.AvailableHistoryPeriods = (await _scheduleRepo.GetAllPeriodsAsync()).OrderByDescending(p => p.StartDate).ToList();
            ViewBag.ShiftTypes = (await _shiftTypeRepo.GetAllAsync()).OrderBy(s => s.StartTime).ToList();
            model.PreviewStartDate = model.StartDate;
            model.PreviewEndDate = model.StartDate.AddDays(days - 1);
            model.EmployeeReadinessList = await GetEmployeeReadinessAsync(model.PreviewStartDate, model.PreviewEndDate);
            return View(model);
        }

        public async Task<IActionResult> Preview(string token)
        {
            if (string.IsNullOrEmpty(token) || !_cache.TryGetValue($"SchedulePreview_{token}", out SchedulePreviewData? previewData) || previewData == null)
            {
                TempData["ErrorMessage"] = "Sesi pratinjau jadwal tidak ditemukan atau telah kedaluwarsa. Silakan generate ulang.";
                return RedirectToAction(nameof(Generate));
            }

            var shiftTypes = (await _shiftTypeRepo.GetAllAsync()).OrderBy(s => s.StartTime).ToList();
            ViewBag.ShiftTypes = shiftTypes;
            ViewBag.PreviewToken = token;

            var vm = BuildPreviewMatrixViewModel(previewData, shiftTypes);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSave(string token)
        {
            if (string.IsNullOrEmpty(token) || !_cache.TryGetValue($"SchedulePreview_{token}", out SchedulePreviewData? previewData) || previewData == null)
            {
                TempData["ErrorMessage"] = "Sesi pratinjau jadwal telah kedaluwarsa. Silakan generate ulang.";
                return RedirectToAction(nameof(Generate));
            }

            var period = await _scheduleService.SavePreviewAsync(previewData);
            _cache.Remove($"SchedulePreview_{token}");

            // Buat & arsipkan berkas PDF resmi ke folder wwwroot/penjadwalan_pdf/
            try
            {
                var managerName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Manager";
                var (pdfBytes, qrVerificationCode) = await _pdfExportService.GenerateSchedulePdfAsync(period.Id, managerName);

                var pdfFolder = Path.Combine(_env.WebRootPath, "penjadwalan_pdf");
                if (!Directory.Exists(pdfFolder))
                {
                    Directory.CreateDirectory(pdfFolder);
                }

                var safeCode = string.IsNullOrWhiteSpace(period.ScheduleCode) ? $"SCH-{period.Id:D4}" : period.ScheduleCode;
                var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                var fileName = $"Jadwal_{safeCode}_{timeStamp}.pdf";
                var filePath = Path.Combine(pdfFolder, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

                period.PdfFilePath = $"/penjadwalan_pdf/{fileName}";
                period.QrVerificationCode = qrVerificationCode;
                await _scheduleRepo.UpdatePeriodAsync(period);
                await _scheduleRepo.SaveChangesAsync();
            }
            catch { }

            TempData["SuccessMessage"] = $"Jadwal '{period.PeriodName}' berhasil disimpan dan otomatis resmi dipublikasikan.";
            return RedirectToAction(nameof(Detail), new { id = period.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RerunPreview(string token)
        {
            if (string.IsNullOrEmpty(token) || !_cache.TryGetValue($"SchedulePreview_{token}", out SchedulePreviewData? prevData) || prevData == null)
            {
                TempData["ErrorMessage"] = "Sesi pratinjau jadwal telah kedaluwarsa. Silakan buat jadwal baru.";
                return RedirectToAction(nameof(Generate));
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            var newPreviewData = await _scheduleService.GenerateSchedulePreviewAsync(
                prevData.StartDate,
                prevData.DurationType,
                prevData.MaxConsecutiveDays,
                prevData.MaxOffDays,
                prevData.PreventNightToMorning,
                prevData.MinSenior,
                prevData.NightShiftId,
                userId > 0 ? userId : prevData.CreatedBy,
                prevData.PreviousPeriodId);

            if (newPreviewData != null && newPreviewData.Details.Any())
            {
                _cache.Remove($"SchedulePreview_{token}");
                var newToken = newPreviewData.Token;
                _cache.Set($"SchedulePreview_{newToken}", newPreviewData, System.TimeSpan.FromHours(2));
                TempData["SuccessMessage"] = "Jadwal berhasil disusun ulang dengan variasi rotasi alternatif!";
                return RedirectToAction(nameof(Preview), new { token = newToken });
            }

            TempData["ErrorMessage"] = "Gagal menyusun ulang jadwal. Silakan coba kembali.";
            return RedirectToAction(nameof(Preview), new { token = token });
        }

        public IActionResult DiscardPreview(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                _cache.Remove($"SchedulePreview_{token}");
            }
            TempData["InfoMessage"] = "Pratinjau jadwal dibatalkan. Tidak ada data yang tersimpan ke database.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Detail(int id)
        {
            var period = await _scheduleRepo.GetPeriodByIdAsync(id);
            if (period == null) return NotFound();

            var details = await _scheduleRepo.GetDetailsByPeriodIdAsync(id);
            var shiftTypes = await _shiftTypeRepo.GetAllAsync();

            var vm = BuildMatrixViewModel(period, details, shiftTypes.ToList());
            ViewBag.ShiftTypes = shiftTypes.OrderBy(s => s.StartTime).ToList();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(int id)
        {
            var period = await _scheduleRepo.GetPeriodByIdAsync(id);
            if (period == null) return NotFound();

            period.IsPublished = true;
            await _scheduleRepo.UpdatePeriodAsync(period);
            await _scheduleRepo.SaveChangesAsync();

            return RedirectToAction(nameof(Detail), new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var period = await _scheduleRepo.GetPeriodByIdAsync(id);
                if (period == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
                        return Json(new { success = false, message = "Periode jadwal tidak ditemukan." });

                    TempData["ErrorMessage"] = "Periode jadwal tidak ditemukan.";
                    return RedirectToAction(nameof(Index));
                }

                // Hapus berkas fisik PDF jika ada di wwwroot/penjadwalan_pdf/
                if (!string.IsNullOrEmpty(period.PdfFilePath))
                {
                    try
                    {
                        var fullPdfPath = Path.Combine(_env.WebRootPath, period.PdfFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(fullPdfPath))
                        {
                            System.IO.File.Delete(fullPdfPath);
                        }
                    }
                    catch { }
                }

                await _scheduleRepo.DeletePeriodAsync(id);
                await _scheduleRepo.SaveChangesAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
                    return Json(new { success = true, message = "Jadwal berhasil dihapus." });

                TempData["SuccessMessage"] = "Jadwal berhasil dihapus.";
                return RedirectToAction(nameof(Index));
            }
            catch (System.Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
                    return Json(new { success = false, message = "Gagal menghapus jadwal: " + ex.Message });

                TempData["ErrorMessage"] = "Gagal menghapus jadwal: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Emergency(int id, int? detailId)
        {
            int targetDetailId = detailId ?? id;
            var detail = await _scheduleRepo.GetDetailByIdAsync(targetDetailId);
            if (detail == null) return NotFound();

            var candidates = await _scheduleService.FindEmergencyReplacementsAsync(detail.SchedulePeriodId, detail.UserId, detail.WorkDate);

            var vm = new EmergencyReplacementViewModel
            {
                PeriodId = detail.SchedulePeriodId,
                DetailId = detail.Id,
                SickUserId = detail.UserId,
                SickUserName = detail.User?.NamaLengkap ?? "Karyawan",
                SickDate = detail.WorkDate,
                ShiftName = detail.ShiftType?.ShiftName ?? "Shift",
                TargetShiftCode = detail.ShiftType?.ShiftCode ?? "S",
                Candidates = candidates.ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignReplacement(int? periodId, int? detailId, int? replacementUserId, int? ScheduleDetailId, int? ReplacementEmployeeId)
        {
            int targetDetailId = detailId ?? ScheduleDetailId ?? 0;
            int targetReplacementId = replacementUserId ?? ReplacementEmployeeId ?? 0;

            var detail = await _scheduleRepo.GetDetailByIdAsync(targetDetailId);
            if (detail == null) return NotFound();

            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var targetPeriodId = periodId ?? detail.SchedulePeriodId;
            var oldShiftTypeId = detail.ShiftTypeId;

            // 1. Mark original detail as Sick
            detail.Status = "Sick";
            detail.UpdatedAt = DateTime.UtcNow;
            detail.UpdatedBy = currentUserId;

            // Check if replacement user already has an OFF detail on that date in this period
            var existingReplacementDetail = (await _scheduleRepo.GetDetailsByPeriodIdAsync(targetPeriodId))
                .FirstOrDefault(d => d.UserId == targetReplacementId && d.WorkDate.Date == detail.WorkDate.Date);

            if (existingReplacementDetail != null)
            {
                existingReplacementDetail.ShiftTypeId = oldShiftTypeId;
                existingReplacementDetail.Status = "Assigned";
                existingReplacementDetail.IsReplacement = true;
                existingReplacementDetail.UpdatedAt = DateTime.UtcNow;
                existingReplacementDetail.UpdatedBy = currentUserId;
                await _scheduleRepo.UpdateDetailAsync(existingReplacementDetail);
            }
            else
            {
                var newDetail = new ScheduleDetail
                {
                    SchedulePeriodId = targetPeriodId,
                    UserId = targetReplacementId,
                    ShiftTypeId = oldShiftTypeId,
                    WorkDate = detail.WorkDate,
                    Status = "Assigned",
                    IsReplacement = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId
                };
                await _scheduleRepo.AddDetailAsync(newDetail);
            }

            await _scheduleRepo.UpdateDetailAsync(detail);
            await _scheduleRepo.SaveChangesAsync();

            TempData["SuccessMessage"] = "Karyawan pengganti berhasil ditugaskan.";
            return RedirectToAction(nameof(Detail), new { id = targetPeriodId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkSick(int detailId)
        {
            var detail = await _scheduleRepo.GetDetailByIdAsync(detailId);
            if (detail == null) return Json(new { success = false, message = "Jadwal tidak ditemukan." });

            detail.Status = "Sick";
            detail.UpdatedAt = DateTime.UtcNow;
            detail.UpdatedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            await _scheduleRepo.UpdateDetailAsync(detail);
            await _scheduleRepo.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkSickOnly(int ScheduleDetailId)
        {
            var detail = await _scheduleRepo.GetDetailByIdAsync(ScheduleDetailId);
            if (detail == null) return NotFound();

            detail.Status = "Sick";
            detail.UpdatedAt = DateTime.UtcNow;
            detail.UpdatedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            await _scheduleRepo.UpdateDetailAsync(detail);
            await _scheduleRepo.SaveChangesAsync();

            TempData["SuccessMessage"] = "Jadwal telah ditandai sakit tanpa pengganti.";
            return RedirectToAction(nameof(Detail), new { id = detail.SchedulePeriodId });
        }

        [AllowAnonymous]
        public async Task<IActionResult> ExportPdf(int id)
        {
            var period = await _scheduleRepo.GetPeriodByIdAsync(id);
            if (period == null) return NotFound();

            // Jika masih draft, hanya Manager/Admin yang boleh ekspor
            if (!period.IsPublished && !(User.Identity?.IsAuthenticated == true && (User.IsInRole("Manager") || User.IsInRole("Admin"))))
            {
                return RedirectToAction("Login", "Account");
            }

            var managerName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Manager";
            var (pdfBytes, qrVerificationCode) = await _pdfExportService.GenerateSchedulePdfAsync(id, managerName);

            // Simpan fisik PDF ke folder wwwroot/penjadwalan_pdf/
            var pdfFolder = Path.Combine(_env.WebRootPath, "penjadwalan_pdf");
            if (!Directory.Exists(pdfFolder))
            {
                Directory.CreateDirectory(pdfFolder);
            }

            var safeCode = string.IsNullOrWhiteSpace(period.ScheduleCode) ? $"SCH-{period.Id:D4}" : period.ScheduleCode;
            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var fileName = $"Jadwal_{safeCode}_{timeStamp}.pdf";
            var filePath = Path.Combine(pdfFolder, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

            period.PdfFilePath = $"/penjadwalan_pdf/{fileName}";
            period.QrVerificationCode = qrVerificationCode;
            await _scheduleRepo.UpdatePeriodAsync(period);
            await _scheduleRepo.SaveChangesAsync();

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            return File(pdfBytes, "application/pdf");
        }

        private ScheduleMatrixViewModel BuildMatrixViewModel(SchedulePeriod period, System.Collections.Generic.IEnumerable<ScheduleDetail> details, System.Collections.Generic.List<ShiftType> shiftTypes)
        {
            var vm = new ScheduleMatrixViewModel
            {
                PeriodId = period.Id,
                ScheduleCode = period.ScheduleCode,
                PeriodName = period.PeriodName,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                IsPublished = period.IsPublished,
                Dates = new System.Collections.Generic.List<System.DateTime>(),
                Rows = new System.Collections.Generic.List<EmployeeScheduleRow>()
            };

            for (var d = period.StartDate; d <= period.EndDate; d = d.AddDays(1))
            {
                vm.Dates.Add(d);
            }

            var grouped = details.GroupBy(d => d.User).ToList();
            foreach (var g in grouped)
            {
                if(g.Key == null) continue;

                var row = new EmployeeScheduleRow
                {
                    UserId = g.Key.Id,
                    NamaLengkap = g.Key.NamaLengkap,
                    Level = g.Key.Level,
                    Days = new System.Collections.Generic.List<DayAssignment>(),
                    TotalShifts = g.Count(d => d.ShiftTypeId != null),
                    NightShifts = g.Count(d => d.ShiftTypeId != null && (d.ShiftTypeId == period.NightShiftId || (d.ShiftType != null && (d.ShiftType.ShiftCode == "M" || d.ShiftType.ShiftName.ToLower().Contains("malam")))))
                };

                foreach (var date in vm.Dates)
                {
                    var d = g.FirstOrDefault(x => x.WorkDate.Date == date.Date);
                    if (d != null)
                    {
                        row.Days.Add(new DayAssignment
                        {
                            DetailId = d.Id,
                            ShiftCode = d.ShiftType?.ShiftCode,
                            ShiftName = d.ShiftType?.ShiftName,
                            Status = d.Status,
                            IsReplacement = d.IsReplacement,
                            IsNightShift = d.ShiftTypeId != null && (d.ShiftTypeId == period.NightShiftId || (d.ShiftType != null && (d.ShiftType.ShiftCode == "M" || d.ShiftType.ShiftName.ToLower().Contains("malam"))))
                        });
                    }
                    else
                    {
                        row.Days.Add(null);
                    }
                }
                vm.Rows.Add(row);
            }

            return vm;
        }

        private ScheduleMatrixViewModel BuildPreviewMatrixViewModel(SchedulePreviewData preview, System.Collections.Generic.List<ShiftType> shiftTypes)
        {
            var vm = new ScheduleMatrixViewModel
            {
                PeriodId = 0,
                ScheduleCode = preview.ScheduleCode,
                PeriodName = preview.PeriodName,
                StartDate = preview.StartDate,
                EndDate = preview.EndDate,
                IsPublished = false,
                Dates = new System.Collections.Generic.List<System.DateTime>(),
                Rows = new System.Collections.Generic.List<EmployeeScheduleRow>()
            };

            for (var d = preview.StartDate.Date; d <= preview.EndDate.Date; d = d.AddDays(1))
            {
                vm.Dates.Add(d);
            }

            var groupedByUser = preview.Details.GroupBy(d => new { d.UserId, d.UserName, d.UserLevel }).ToList();
            foreach (var g in groupedByUser)
            {
                var row = new EmployeeScheduleRow
                {
                    UserId = g.Key.UserId,
                    NamaLengkap = g.Key.UserName,
                    Level = g.Key.UserLevel,
                    Days = new System.Collections.Generic.List<DayAssignment>(),
                    TotalShifts = g.Count(d => d.ShiftTypeId != null && d.Status == "Assigned"),
                    NightShifts = g.Count(d => d.IsNightShift && d.Status == "Assigned")
                };

                foreach (var date in vm.Dates)
                {
                    var item = g.FirstOrDefault(x => x.WorkDate.Date == date.Date);
                    if (item != null)
                    {
                        row.Days.Add(new DayAssignment
                        {
                            DetailId = 0,
                            ShiftCode = item.ShiftCode ?? (item.Status == "Off" ? "OFF" : item.Status),
                            ShiftName = item.ShiftName,
                            Status = item.Status,
                            IsReplacement = item.IsReplacement,
                            IsNightShift = item.IsNightShift
                        });
                    }
                    else
                    {
                        row.Days.Add(new DayAssignment
                        {
                            DetailId = 0,
                            ShiftCode = "OFF",
                            ShiftName = "Libur",
                            Status = "Off",
                            IsReplacement = false,
                            IsNightShift = false
                        });
                    }
                }
                vm.Rows.Add(row);
            }

            return vm;
        }
    }
}
