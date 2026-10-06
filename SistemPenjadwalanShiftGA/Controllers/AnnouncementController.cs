using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SistemPenjadwalanShiftGA.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class AnnouncementController : Controller
    {
        private readonly IAnnouncementRepository _announcementRepo;

        public AnnouncementController(IAnnouncementRepository announcementRepo)
        {
            _announcementRepo = announcementRepo;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var announcements = await _announcementRepo.GetAllAsync();
            var data = announcements.Select(a => new
            {
                id = a.Id,
                title = a.Title,
                content = a.Content,
                priority = a.Priority,
                createdByName = a.User?.NamaLengkap ?? "Manager",
                createdAt = a.CreatedAt.ToString("o")
            });
            return Json(new { data });
        }

        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var a = await _announcementRepo.GetByIdAsync(id);
            if (a == null) return Json(new { success = false, message = "Pengumuman tidak ditemukan." });

            return Json(new
            {
                success = true,
                data = new
                {
                    id = a.Id,
                    title = a.Title,
                    content = a.Content,
                    priority = a.Priority
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Announcement model)
        {
            if (ModelState.IsValid)
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int.TryParse(userIdStr, out int userId);
                
                model.UserId = userId;
                model.CreatedAt = DateTime.UtcNow;
                model.CreatedBy = userId;

                await _announcementRepo.AddAsync(model);
                await _announcementRepo.SaveChangesAsync();
                
                return Json(new { success = true, message = "Announcement created successfully." });
            }
            return Json(new { success = false, message = "Invalid data." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Announcement model)
        {
            if (id != model.Id) return Json(new { success = false, message = "ID mismatch." });

            var existing = await _announcementRepo.GetByIdAsync(id);
            if (existing == null) return Json(new { success = false, message = "Announcement not found." });

            existing.Title = model.Title;
            existing.Content = model.Content;
            existing.Priority = model.Priority;

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;

            await _announcementRepo.UpdateAsync(existing);
            await _announcementRepo.SaveChangesAsync();

            return Json(new { success = true, message = "Announcement updated successfully." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _announcementRepo.GetByIdAsync(id);
            if (existing == null) return Json(new { success = false, message = "Announcement not found." });

            await _announcementRepo.DeleteAsync(id);
            await _announcementRepo.SaveChangesAsync();
            return Json(new { success = true, message = "Announcement deleted successfully." });
        }
    }
}
