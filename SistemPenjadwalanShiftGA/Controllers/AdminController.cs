using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemPenjadwalanShiftGA.Data;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;
using SistemPenjadwalanShiftGA.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace SistemPenjadwalanShiftGA.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IAnnouncementRepository _announcementRepo;
        private readonly IShiftTypeRepository _shiftTypeRepo;
        private readonly ILeaveRequestRepository _leaveRequestRepo;
        private readonly IScheduleRepository _scheduleRepo;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            AppDbContext context,
            IUserRepository userRepo,
            IRoleRepository roleRepo,
            IAnnouncementRepository announcementRepo,
            IShiftTypeRepository shiftTypeRepo,
            ILeaveRequestRepository leaveRequestRepo,
            IScheduleRepository scheduleRepo,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _announcementRepo = announcementRepo;
            _shiftTypeRepo = shiftTypeRepo;
            _leaveRequestRepo = leaveRequestRepo;
            _scheduleRepo = scheduleRepo;
            _logger = logger;
        }

        // ==================== PAGES ====================

        public async Task<IActionResult> Dashboard()
        {
            var vm = new DashboardViewModel
            {
                TotalKaryawan = await _context.Users.CountAsync(u => u.RoleId == 2 && u.IsActive),
                TotalShiftTypes = await _context.ShiftTypes.CountAsync(),
                TotalPeriods = await _context.SchedulePeriods.CountAsync(),
                PendingLeaveRequests = await _context.LeaveRequests.CountAsync(l => l.Status == "Pending"),
                PublishedPeriods = await _context.SchedulePeriods.CountAsync(p => p.IsPublished),
                RecentAnnouncements = await _context.Announcements
                    .Include(a => a.User)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(5)
                    .ToListAsync(),
                ActivePeriod = await _context.SchedulePeriods
                    .Where(p => p.IsPublished && p.EndDate >= System.DateTime.Today)
                    .OrderByDescending(p => p.StartDate)
                    .FirstOrDefaultAsync(),
                RecentLeaveRequests = await _context.LeaveRequests
                    .Include(l => l.User)
                    .Where(l => l.Status == "Pending")
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(7)
                    .ToListAsync()
            };
            ViewData["Title"] = "Dashboard";
            return View(vm);
        }

        // ==================== JSON API ====================

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var vm = new 
            {
                TotalKaryawan = await _context.Users.CountAsync(u => u.RoleId == 2 && u.IsActive),
                TotalShiftTypes = await _context.ShiftTypes.CountAsync(),
                TotalPeriods = await _context.SchedulePeriods.CountAsync(),
                PendingLeaveRequests = await _context.LeaveRequests.CountAsync(l => l.Status == "Pending"),
                PublishedPeriods = await _context.SchedulePeriods.CountAsync(p => p.IsPublished)
            };
            return Json(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .OrderBy(u => u.NamaLengkap)
                .Select(u => new {
                    u.Id,
                    u.NamaLengkap,
                    u.Email,
                    u.RoleId,
                    RoleName = u.Role != null ? u.Role.NamaRole : "-",
                    u.IsActive,
                    CreatedAt = u.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();
            return Json(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles
                .OrderBy(r => r.NamaRole)
                .Select(r => new {
                    r.Id,
                    r.NamaRole,
                    Deskripsi = r.Deskripsi ?? "-"
                })
                .ToListAsync();
            return Json(roles);
        }

        // ==================== USERS CRUD ====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(User user, string ConfirmPassword)
        {
            if (string.IsNullOrWhiteSpace(user.NamaLengkap))
                return Json(new { success = false, message = "Nama lengkap wajib diisi." });
            if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains('@'))
                return Json(new { success = false, message = "Email tidak valid." });
            if (string.IsNullOrWhiteSpace(user.PasswordHash) || user.PasswordHash.Length < 6)
                return Json(new { success = false, message = "Password minimal 6 karakter." });
            if (ConfirmPassword != user.PasswordHash)
                return Json(new { success = false, message = "Password dan konfirmasi tidak cocok." });
            if (user.RoleId <= 0)
                return Json(new { success = false, message = "Pilih role terlebih dahulu." });

            var existing = await _userRepo.GetByEmailAsync(user.Email);
            if (existing != null)
                return Json(new { success = false, message = "Email sudah terdaftar." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;

            await _userRepo.AddAsync(user);
            await _userRepo.SaveChangesAsync();

            return Json(new { success = true, message = "Berhasil menambah user baru." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, User user, string? Password)
        {
            if (id != user.Id)
                return Json(new { success = false, message = "ID tidak cocok." });

            var current = await _userRepo.GetByIdAsync(id);
            if (current == null)
                return Json(new { success = false, message = "User tidak ditemukan." });

            if (string.IsNullOrWhiteSpace(user.NamaLengkap))
                return Json(new { success = false, message = "Nama lengkap wajib diisi." });
            if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains('@'))
                return Json(new { success = false, message = "Email tidak valid." });

            var emailConflict = await _userRepo.GetByEmailAsync(user.Email);
            if (emailConflict != null && emailConflict.Id != id)
                return Json(new { success = false, message = "Email sudah digunakan user lain." });

            current.NamaLengkap = user.NamaLengkap;
            current.Email = user.Email;
            current.RoleId = user.RoleId;
            current.IsActive = user.IsActive;
            current.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(Password) && Password.Length >= 6)
                current.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

            await _userRepo.UpdateAsync(current);
            await _userRepo.SaveChangesAsync();

            return Json(new { success = true, message = "Berhasil memperbarui data user." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null)
                return Json(new { success = false, message = "User tidak ditemukan." });

            await _userRepo.DeleteAsync(id);
            await _userRepo.SaveChangesAsync();

            return Json(new { success = true, message = "Berhasil menghapus user." });
        }

        // ==================== ROLES CRUD ====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(Role role)
        {
            if (string.IsNullOrWhiteSpace(role.NamaRole))
                return Json(new { success = false, message = "Nama role wajib diisi." });

            var existing = await _roleRepo.GetAllAsync();
            if (existing.Any(r => r.NamaRole.Equals(role.NamaRole, StringComparison.OrdinalIgnoreCase)))
                return Json(new { success = false, message = "Nama role sudah ada." });

            await _roleRepo.AddAsync(role);
            await _roleRepo.SaveChangesAsync();

            return Json(new { success = true, message = "Berhasil menambah role baru." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(int id, Role role)
        {
            if (id != role.Id)
                return Json(new { success = false, message = "ID tidak cocok." });

            var current = await _roleRepo.GetByIdAsync(id);
            if (current == null)
                return Json(new { success = false, message = "Role tidak ditemukan." });

            if (string.IsNullOrWhiteSpace(role.NamaRole))
                return Json(new { success = false, message = "Nama role wajib diisi." });

            var allRoles = await _roleRepo.GetAllAsync();
            if (allRoles.Any(r => r.NamaRole.Equals(role.NamaRole, StringComparison.OrdinalIgnoreCase) && r.Id != id))
                return Json(new { success = false, message = "Nama role sudah digunakan." });

            current.NamaRole = role.NamaRole;
            current.Deskripsi = role.Deskripsi;

            await _roleRepo.UpdateAsync(current);
            await _roleRepo.SaveChangesAsync();

            return Json(new { success = true, message = "Berhasil memperbarui role." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var hasUsers = await _roleRepo.HasUsersAsync(id);
            if (hasUsers)
                return Json(new { success = false, message = "Tidak bisa menghapus role yang masih digunakan oleh user." });

            await _roleRepo.DeleteAsync(id);
            await _roleRepo.SaveChangesAsync();

            return Json(new { success = true, message = "Berhasil menghapus role." });
        }
    }
}
