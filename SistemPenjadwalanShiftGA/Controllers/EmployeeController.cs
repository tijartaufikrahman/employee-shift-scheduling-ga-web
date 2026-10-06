using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SistemPenjadwalanShiftGA.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class EmployeeController : Controller
    {
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;

        public EmployeeController(IUserRepository userRepo, IRoleRepository roleRepo)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Only list employees (RoleId=2 Karyawan) according to prompt
            // Actually I should just use GetAllAsync and filter, wait, no filter method on repository in the interface list,
            // but I can filter in memory or wait, _userRepo usually has queryable or list. I'll get all and filter.
            var users = await _userRepo.GetAllAsync();
            var employees = users.Where(u => u.RoleId == 2).Select(u => new {
                u.Id,
                u.NamaLengkap,
                u.Email,
                u.PhoneNumber,
                u.Level,
                u.IsActive,
                CreatedAt = u.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            }).ToList();

            return Json(new { data = employees });
        }

        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var u = await _userRepo.GetByIdAsync(id);
            if (u == null) return Json(new { success = false, message = "Karyawan tidak ditemukan." });

            return Json(new
            {
                success = true,
                data = new
                {
                    id = u.Id,
                    namaLengkap = u.NamaLengkap,
                    email = u.Email,
                    phoneNumber = u.PhoneNumber,
                    level = u.Level,
                    isActive = u.IsActive
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User model, string Password, string ConfirmPassword)
        {
            if (string.IsNullOrWhiteSpace(Password) || Password != ConfirmPassword)
                return Json(new { success = false, message = "Passwords do not match or are empty." });

            if (ModelState.IsValid)
            {
                model.RoleId = 2; // Karyawan
                model.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
                
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int.TryParse(userIdStr, out int currentUserId);

                model.CreatedAt = DateTime.UtcNow;
                model.CreatedBy = currentUserId;

                await _userRepo.AddAsync(model);
                await _userRepo.SaveChangesAsync();
                
                return Json(new { success = true, message = "Employee created successfully." });
            }
            return Json(new { success = false, message = "Invalid data." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User model, string? Password)
        {
            if (id != model.Id) return Json(new { success = false, message = "ID mismatch." });

            var existing = await _userRepo.GetByIdAsync(id);
            if (existing == null) return Json(new { success = false, message = "Employee not found." });

            existing.NamaLengkap = model.NamaLengkap;
            existing.Email = model.Email;
            existing.PhoneNumber = model.PhoneNumber;
            existing.Level = model.Level;
            existing.IsActive = model.IsActive;

            if (!string.IsNullOrWhiteSpace(Password))
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int currentUserId);
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUserId;

            await _userRepo.UpdateAsync(existing);
            await _userRepo.SaveChangesAsync();

            return Json(new { success = true, message = "Employee updated successfully." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _userRepo.GetByIdAsync(id);
            if (existing == null) return Json(new { success = false, message = "Employee not found." });

            await _userRepo.DeleteAsync(id);
            await _userRepo.SaveChangesAsync();
            return Json(new { success = true, message = "Employee deleted successfully." });
        }
    }
}
