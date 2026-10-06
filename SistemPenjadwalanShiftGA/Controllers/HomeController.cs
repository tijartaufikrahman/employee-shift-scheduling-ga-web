using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models;
using SistemPenjadwalanShiftGA.Models.ViewModels;
using System.Diagnostics;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;

namespace SistemPenjadwalanShiftGA.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IAnnouncementRepository _announcementRepo;
        private readonly ILeaveRequestRepository _leaveRequestRepo;
        private readonly IScheduleRepository _scheduleRepo;
        private readonly IShiftTypeRepository _shiftTypeRepo;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IAnnouncementRepository announcementRepo,
            ILeaveRequestRepository leaveRequestRepo,
            IScheduleRepository scheduleRepo,
            IShiftTypeRepository shiftTypeRepo,
            ILogger<HomeController> logger)
        {
            _announcementRepo = announcementRepo;
            _leaveRequestRepo = leaveRequestRepo;
            _scheduleRepo = scheduleRepo;
            _shiftTypeRepo = shiftTypeRepo;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role == "Admin" || role == "Manager")
                return RedirectToAction("Dashboard", "Admin");

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);

            var allAnnouncements = await _announcementRepo.GetAllAsync();
            var recentAnnouncements = allAnnouncements.OrderByDescending(a => a.CreatedAt).Take(10).ToList();

            var userLeaveRequests = await _leaveRequestRepo.GetByUserIdAsync(userId);
            
            var periods = await _scheduleRepo.GetAllPeriodsAsync();
            var activePeriod = periods
                .Where(p => p.IsPublished && p.EndDate >= System.DateTime.Today)
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefault();

            ScheduleMatrixViewModel mySchedule = null;
            if (activePeriod != null)
            {
                var details = await _scheduleRepo.GetDetailsByPeriodIdAsync(activePeriod.Id);
                var userDetails = details.Where(d => d.UserId == userId).ToList();
                
                mySchedule = new ScheduleMatrixViewModel
                {
                    PeriodId = activePeriod.Id,
                    PeriodName = activePeriod.PeriodName,
                    StartDate = activePeriod.StartDate,
                    EndDate = activePeriod.EndDate,
                    IsPublished = activePeriod.IsPublished,
                    Dates = new System.Collections.Generic.List<System.DateTime>(),
                    Rows = new System.Collections.Generic.List<EmployeeScheduleRow>()
                };

                for (var d = activePeriod.StartDate; d <= activePeriod.EndDate; d = d.AddDays(1))
                {
                    mySchedule.Dates.Add(d);
                }

                var row = new EmployeeScheduleRow
                {
                    UserId = userId,
                    Days = new System.Collections.Generic.List<DayAssignment>(),
                    TotalShifts = userDetails.Count(d => d.ShiftTypeId != null),
                    NightShifts = userDetails.Count(d => d.ShiftTypeId != null && d.ShiftTypeId == activePeriod.NightShiftId)
                };

                foreach (var date in mySchedule.Dates)
                {
                    var d = userDetails.FirstOrDefault(x => x.WorkDate.Date == date.Date);
                    if (d != null)
                    {
                        row.Days.Add(new DayAssignment
                        {
                            DetailId = d.Id,
                            ShiftCode = d.ShiftType?.ShiftCode,
                            ShiftName = d.ShiftType?.ShiftName,
                            Status = d.Status,
                            IsReplacement = d.IsReplacement,
                            IsNightShift = d.ShiftTypeId != null && d.ShiftTypeId == activePeriod.NightShiftId
                        });
                    }
                    else
                    {
                        row.Days.Add(null);
                    }
                }
                mySchedule.Rows.Add(row);
            }

            var myScheduleItems = new List<MyScheduleItem>();
            if (activePeriod != null && mySchedule != null && mySchedule.Rows.Any())
            {
                var myRow = mySchedule.Rows.First();
                for (int i = 0; i < mySchedule.Dates.Count; i++)
                {
                    var day = myRow.Days.Count > i ? myRow.Days[i] : null;
                    myScheduleItems.Add(new MyScheduleItem
                    {
                        Date = mySchedule.Dates[i],
                        ShiftCode = day?.ShiftCode ?? "OFF",
                        IsSick = day?.Status == "Sick"
                    });
                }
            }

            var vm = new EmployeeDashboardViewModel
            {
                EmployeeName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Karyawan",
                ActiveSchedulePeriod = activePeriod,
                MySchedule = myScheduleItems,
                Announcements = recentAnnouncements,
                MyLeaveRequests = userLeaveRequests.ToList(),
                MyCurrentSchedule = mySchedule
            };

            ViewData["Title"] = "Jadwal Saya";
            return View(vm);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
