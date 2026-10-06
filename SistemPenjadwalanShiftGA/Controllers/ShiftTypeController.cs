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
    public class ShiftTypeController : Controller
    {
        private readonly IShiftTypeRepository _shiftTypeRepo;

        public ShiftTypeController(IShiftTypeRepository shiftTypeRepo)
        {
            _shiftTypeRepo = shiftTypeRepo;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var shiftTypes = await _shiftTypeRepo.GetAllAsync();
            var data = shiftTypes.Select(s => new
            {
                id = s.Id,
                shiftName = s.ShiftName,
                shiftCode = s.ShiftCode,
                startTime = s.StartTime.ToString(@"hh\:mm"),
                endTime = s.EndTime.ToString(@"hh\:mm"),
                minStaff = s.MinStaff
            });
            return Json(new { data });
        }

        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var s = await _shiftTypeRepo.GetByIdAsync(id);
            if (s == null) return Json(new { success = false, message = "Tipe shift tidak ditemukan." });

            return Json(new
            {
                success = true,
                data = new
                {
                    id = s.Id,
                    shiftName = s.ShiftName,
                    shiftCode = s.ShiftCode,
                    startTime = s.StartTime.ToString(@"hh\:mm"),
                    endTime = s.EndTime.ToString(@"hh\:mm"),
                    minStaff = s.MinStaff
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShiftType model)
        {
            if (ModelState.IsValid)
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int.TryParse(userIdStr, out int userId);
                
                model.CreatedAt = DateTime.UtcNow;
                model.CreatedBy = userId;

                await _shiftTypeRepo.AddAsync(model);
                await _shiftTypeRepo.SaveChangesAsync();
                
                return Json(new { success = true, message = "Shift Type created successfully." });
            }
            return Json(new { success = false, message = "Invalid data." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ShiftType model)
        {
            if (id != model.Id) return Json(new { success = false, message = "ID mismatch." });

            if (ModelState.IsValid)
            {
                var existing = await _shiftTypeRepo.GetByIdAsync(id);
                if (existing == null) return Json(new { success = false, message = "Shift Type not found." });

                existing.ShiftName = model.ShiftName;
                existing.ShiftCode = model.ShiftCode;
                existing.StartTime = model.StartTime;
                existing.EndTime = model.EndTime;
                existing.MinStaff = model.MinStaff;
                
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int.TryParse(userIdStr, out int userId);
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = userId;

                await _shiftTypeRepo.UpdateAsync(existing);
                await _shiftTypeRepo.SaveChangesAsync();

                return Json(new { success = true, message = "Shift Type updated successfully." });
            }
            return Json(new { success = false, message = "Invalid data." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _shiftTypeRepo.GetByIdAsync(id);
            if (existing == null) return Json(new { success = false, message = "Shift Type not found." });

            await _shiftTypeRepo.DeleteAsync(id);
            await _shiftTypeRepo.SaveChangesAsync();
            return Json(new { success = true, message = "Shift Type deleted successfully." });
        }
    }
}
