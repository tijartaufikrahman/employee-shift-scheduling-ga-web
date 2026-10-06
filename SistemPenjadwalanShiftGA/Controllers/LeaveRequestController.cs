using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.ViewModels;
using SistemPenjadwalanShiftGA.Models.Entities;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SistemPenjadwalanShiftGA.Controllers
{
    [Authorize]
    public class LeaveRequestController : Controller
    {
        private readonly ILeaveRequestRepository _leaveRequestRepo;
        private readonly IWebHostEnvironment _env;

        public LeaveRequestController(ILeaveRequestRepository leaveRequestRepo, IWebHostEnvironment env)
        {
            _leaveRequestRepo = leaveRequestRepo;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            if (role == "Manager" || role == "Admin")
            {
                var allRequests = await _leaveRequestRepo.GetAllAsync();
                return View(allRequests);
            }
            else
            {
                var myRequests = await _leaveRequestRepo.GetByUserIdAsync(userId);
                return View(myRequests);
            }
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeaveRequestCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int.TryParse(userIdStr, out int userId);

                var leaveRequest = new LeaveRequest
                {
                    UserId = userId,
                    LeaveType = model.LeaveType,
                    RequestDate = model.RequestDate,
                    Reason = model.Reason,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                await _leaveRequestRepo.AddAsync(leaveRequest);
                await _leaveRequestRepo.SaveChangesAsync(); // To get the Id

                if (model.Attachments != null && model.Attachments.Count > 0)
                {
                    var uploadDir = Path.Combine(_env.WebRootPath, "upload");
                    if (!Directory.Exists(uploadDir))
                    {
                        Directory.CreateDirectory(uploadDir);
                    }

                    foreach (var file in model.Attachments)
                    {
                        if (file != null && file.Length > 0)
                        {
                            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                            var originalName = Path.GetFileName(file.FileName);
                            var cleanName = string.Join("_", originalName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                            var safeFileName = $"{timeStamp}_{cleanName}";

                            var targetPath = Path.Combine(uploadDir, safeFileName);
                            int count = 1;
                            var nameWithoutExt = Path.GetFileNameWithoutExtension(cleanName);
                            var ext = Path.GetExtension(cleanName);
                            while (System.IO.File.Exists(targetPath))
                            {
                                safeFileName = $"{timeStamp}_{nameWithoutExt}_{count}{ext}";
                                targetPath = Path.Combine(uploadDir, safeFileName);
                                count++;
                            }

                            using (var stream = new FileStream(targetPath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            var attachment = new LeaveRequestAttachment
                            {
                                LeaveRequestId = leaveRequest.Id,
                                FileName = originalName,
                                FilePath = $"/upload/{safeFileName}",
                                FileSize = (int)file.Length,
                                FileType = file.ContentType,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = userId
                            };
                            await _leaveRequestRepo.AddAttachmentAsync(attachment);
                        }
                    }
                    await _leaveRequestRepo.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Permohonan izin baru berhasil diajukan dan sedang menunggu persetujuan Manager.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Mohon periksa kembali isian formulir permohonan izin Anda.";
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            var leaveRequest = await _leaveRequestRepo.GetByIdAsync(id);
            if (leaveRequest == null) return NotFound();

            bool isManager = role == "Manager" || role == "Admin";
            if (!isManager && leaveRequest.UserId != userId)
                return Forbid();

            if (leaveRequest.Status != "Pending")
            {
                string statusIndo = leaveRequest.Status == "Approved" ? "disetujui" : "ditolak";
                TempData["ErrorMessage"] = $"Permohonan izin tidak dapat diubah karena sudah {statusIndo} oleh Manager.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            var vm = new LeaveRequestEditViewModel
            {
                Id = leaveRequest.Id,
                LeaveType = leaveRequest.LeaveType,
                RequestDate = leaveRequest.RequestDate,
                Reason = leaveRequest.Reason,
                ExistingAttachments = leaveRequest.Attachments.ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(LeaveRequestEditViewModel model)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            var leaveRequest = await _leaveRequestRepo.GetByIdAsync(model.Id);
            if (leaveRequest == null) return NotFound();

            bool isManager = role == "Manager" || role == "Admin";
            if (!isManager && leaveRequest.UserId != userId)
                return Forbid();

            if (leaveRequest.Status != "Pending")
            {
                string statusIndo = leaveRequest.Status == "Approved" ? "disetujui" : "ditolak";
                TempData["ErrorMessage"] = $"Permohonan izin tidak dapat diubah karena sudah {statusIndo} oleh Manager.";
                return RedirectToAction(nameof(Detail), new { id = model.Id });
            }

            if (ModelState.IsValid)
            {
                leaveRequest.LeaveType = model.LeaveType;
                leaveRequest.RequestDate = model.RequestDate;
                leaveRequest.Reason = model.Reason;
                leaveRequest.UpdatedAt = DateTime.UtcNow;
                leaveRequest.UpdatedBy = userId;

                if (model.NewAttachments != null && model.NewAttachments.Count > 0)
                {
                    var uploadDir = Path.Combine(_env.WebRootPath, "upload");
                    if (!Directory.Exists(uploadDir))
                    {
                        Directory.CreateDirectory(uploadDir);
                    }

                    foreach (var file in model.NewAttachments)
                    {
                        if (file != null && file.Length > 0)
                        {
                            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                            var originalName = Path.GetFileName(file.FileName);
                            var cleanName = string.Join("_", originalName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                            var safeFileName = $"{timeStamp}_{cleanName}";

                            var targetPath = Path.Combine(uploadDir, safeFileName);
                            int count = 1;
                            var nameWithoutExt = Path.GetFileNameWithoutExtension(cleanName);
                            var ext = Path.GetExtension(cleanName);
                            while (System.IO.File.Exists(targetPath))
                            {
                                safeFileName = $"{timeStamp}_{nameWithoutExt}_{count}{ext}";
                                targetPath = Path.Combine(uploadDir, safeFileName);
                                count++;
                            }

                            using (var stream = new FileStream(targetPath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            var attachment = new LeaveRequestAttachment
                            {
                                LeaveRequestId = leaveRequest.Id,
                                FileName = originalName,
                                FilePath = $"/upload/{safeFileName}",
                                FileSize = (int)file.Length,
                                FileType = file.ContentType,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = userId
                            };
                            await _leaveRequestRepo.AddAttachmentAsync(attachment);
                        }
                    }
                }

                await _leaveRequestRepo.UpdateAsync(leaveRequest);
                await _leaveRequestRepo.SaveChangesAsync();

                TempData["SuccessMessage"] = "Permohonan izin berhasil diperbarui.";
                return RedirectToAction(nameof(Detail), new { id = leaveRequest.Id });
            }

            model.ExistingAttachments = leaveRequest.Attachments.ToList();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            var leaveRequest = await _leaveRequestRepo.GetByIdAsync(id);
            if (leaveRequest == null)
            {
                if (IsAjaxRequest())
                    return Json(new { success = false, message = "Permohonan izin tidak ditemukan." });
                TempData["ErrorMessage"] = "Permohonan izin tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            bool isManager = role == "Manager" || role == "Admin";

            // Validasi hak akses: hanya pemilik permohonan atau Manager/Admin
            if (!isManager && leaveRequest.UserId != userId)
            {
                if (IsAjaxRequest())
                    return Json(new { success = false, message = "Anda tidak memiliki izin menghapus permohonan ini." });
                TempData["ErrorMessage"] = "Anda tidak memiliki hak untuk menghapus permohonan ini.";
                return RedirectToAction(nameof(Index));
            }

            // Jika karyawan biasa, hanya bisa membatalkan permohonan yang masih Pending
            if (!isManager && leaveRequest.Status != "Pending")
            {
                string statusIndo = leaveRequest.Status == "Approved" ? "disetujui" : "ditolak";
                string msg = $"Permohonan izin tidak dapat dihapus karena sudah {statusIndo} oleh Manager.";
                if (IsAjaxRequest())
                    return Json(new { success = false, message = msg });
                TempData["ErrorMessage"] = msg;
                return RedirectToAction(nameof(Detail), new { id });
            }

            try
            {
                if (leaveRequest.Attachments != null && leaveRequest.Attachments.Any())
                {
                    foreach (var att in leaveRequest.Attachments)
                    {
                        if (!string.IsNullOrEmpty(att.FilePath))
                        {
                            var fullPath = Path.Combine(_env.WebRootPath, att.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                            if (System.IO.File.Exists(fullPath))
                            {
                                System.IO.File.Delete(fullPath);
                            }
                        }
                    }
                }
                var legacyDir = Path.Combine(_env.WebRootPath, "uploads", "leave", leaveRequest.Id.ToString());
                if (Directory.Exists(legacyDir))
                {
                    Directory.Delete(legacyDir, true);
                }
            }
            catch { }

            await _leaveRequestRepo.DeleteAsync(id);
            await _leaveRequestRepo.SaveChangesAsync();

            if (IsAjaxRequest())
            {
                return Json(new { success = true, message = "Permohonan izin berhasil dibatalkan dan dihapus dari sistem." });
            }

            TempData["SuccessMessage"] = "Permohonan izin berhasil dibatalkan dan dihapus.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            var allRequests = await _leaveRequestRepo.GetAllAsync();
            var targetRequest = allRequests.FirstOrDefault(r => r.Attachments.Any(a => a.Id == id));
            if (targetRequest == null)
            {
                return Json(new { success = false, message = "Berkas lampiran tidak ditemukan." });
            }

            bool isManager = role == "Manager" || role == "Admin";
            if (!isManager && targetRequest.UserId != userId)
            {
                return Json(new { success = false, message = "Anda tidak memiliki izin menghapus lampiran ini." });
            }

            if (targetRequest.Status != "Pending")
            {
                return Json(new { success = false, message = "Lampiran tidak dapat dihapus karena permohonan sudah diproses Manager." });
            }

            var att = targetRequest.Attachments.FirstOrDefault(a => a.Id == id);
            if (att != null)
            {
                try
                {
                    var fullPath = Path.Combine(_env.WebRootPath, att.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                catch { }

                await _leaveRequestRepo.DeleteAttachmentAsync(id);
                await _leaveRequestRepo.SaveChangesAsync();
            }

            return Json(new { success = true, message = "Lampiran berhasil dihapus." });
        }

        [HttpGet]
        public async Task<IActionResult> GetListData(string? status)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            IEnumerable<LeaveRequest> list;
            if (role == "Manager" || role == "Admin")
            {
                list = await _leaveRequestRepo.GetAllAsync();
            }
            else
            {
                list = await _leaveRequestRepo.GetByUserIdAsync(userId);
            }

            if (!string.IsNullOrEmpty(status))
            {
                list = list.Where(l => l.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            var data = list.OrderByDescending(l => l.CreatedAt).Select(l => new
            {
                id = l.Id,
                userId = l.UserId,
                employeeName = l.User?.NamaLengkap ?? "-",
                leaveType = l.LeaveType,
                requestDate = l.RequestDate.ToString("yyyy-MM-dd"),
                reason = l.Reason,
                status = l.Status,
                attachmentsCount = l.Attachments?.Count ?? 0,
                canEdit = l.Status == "Pending" && (l.UserId == userId || role == "Manager" || role == "Admin"),
                canDelete = (role == "Manager" || role == "Admin") || (l.UserId == userId && l.Status == "Pending")
            });

            return Json(new { data });
        }

        public async Task<IActionResult> Detail(int id)
        {
            var request = await _leaveRequestRepo.GetByIdAsync(id);
            if (request == null) return NotFound();

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            if (role != "Manager" && role != "Admin" && request.UserId != userId)
                return Forbid();

            var vm = new LeaveRequestDetailViewModel
            {
                Id = request.Id,
                EmployeeName = request.User?.NamaLengkap ?? "Karyawan",
                LeaveType = request.LeaveType,
                RequestDate = request.RequestDate,
                Status = request.Status,
                CreatedAt = request.CreatedAt,
                Reason = request.Reason,
                ReviewedByName = request.UpdatedBy.HasValue ? "Manager" : null,
                ReviewedAt = request.UpdatedAt,
                RejectionNote = request.RejectionNote,
                Attachments = request.Attachments
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(LeaveRequestReviewViewModel model)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Manager" && role != "Admin")
                return Forbid();

            var request = await _leaveRequestRepo.GetByIdAsync(model.Id);
            if (request == null) return NotFound();

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            if (model.Status == "Approved")
            {
                request.Status = "Approved";
                request.RejectionNote = null; // Pastikan catatan penolakan selalu bersih jika disetujui
            }
            else if (model.Status == "Rejected")
            {
                request.Status = "Rejected";
                request.RejectionNote = string.IsNullOrWhiteSpace(model.RejectionNote)
                    ? "Permohonan tidak disetujui oleh Manager."
                    : model.RejectionNote.Trim();
            }
            else if (model.Status == "Pending")
            {
                request.Status = "Pending";
                request.RejectionNote = null;
            }

            request.UpdatedAt = DateTime.UtcNow;
            request.UpdatedBy = userId;

            await _leaveRequestRepo.UpdateAsync(request);
            await _leaveRequestRepo.SaveChangesAsync();

            string statusText = request.Status == "Approved" ? "disetujui" : (request.Status == "Rejected" ? "ditolak" : "dikembalikan ke status pending");
            TempData["SuccessMessage"] = $"Permohonan izin karyawan berhasil {statusText}.";
            return RedirectToAction(nameof(Detail), new { id = model.Id });
        }

        private bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                   (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase));
        }
    }
}
