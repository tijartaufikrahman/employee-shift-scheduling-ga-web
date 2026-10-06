using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Interfaces
{
    public class EmergencyCandidate
    {
        public int UserId { get; set; }
        public int EmployeeId { get => UserId; set => UserId = value; }
        public string NamaLengkap { get; set; }
        public string Level { get; set; }
        public int TotalShiftsInPeriod { get; set; }
    }

    public class SchedulePreviewData
    {
        public string Token { get; set; } = Guid.NewGuid().ToString("N");
        public string ScheduleCode { get; set; } = string.Empty;
        private string _periodName = string.Empty;
        public string PeriodName
        {
            get => System.Text.RegularExpressions.Regex.Replace(_periodName ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            set => _periodName = System.Text.RegularExpressions.Regex.Replace(value ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DurationType { get; set; } = "1_Week";
        public int TotalDays { get; set; }
        public int MaxConsecutiveDays { get; set; } = 5;
        public int MaxOffDays { get; set; } = 2;
        public bool PreventNightToMorning { get; set; } = true;
        public int MinSenior { get; set; } = 1;
        public int? NightShiftId { get; set; }
        public int? PreviousPeriodId { get; set; }
        public int CreatedBy { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow.AddHours(7);
        public List<ScheduleDetailPreviewItem> Details { get; set; } = new();
    }

    public class ScheduleDetailPreviewItem
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserLevel { get; set; } = string.Empty;
        public int? ShiftTypeId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public bool IsNightShift { get; set; }
        public DateTime WorkDate { get; set; }
        public string Status { get; set; } = "Assigned";
        public bool IsReplacement { get; set; }
    }

    public interface IScheduleService
    {
        Task<SchedulePreviewData> GenerateSchedulePreviewAsync(DateTime startDate, string durationType, int maxConsecutiveDays, int maxOffDays, bool preventNightToMorning, int minSenior, int? nightShiftId, int createdBy, int? previousPeriodId = null);
        Task<SchedulePeriod> SavePreviewAsync(SchedulePreviewData previewData);
        Task<SchedulePeriod> GenerateScheduleAsync(DateTime startDate, string durationType, int maxConsecutiveDays, int maxOffDays, bool preventNightToMorning, int minSenior, int? nightShiftId, int createdBy, int? previousPeriodId = null);
        Task<List<EmergencyCandidate>> FindEmergencyReplacementsAsync(int periodId, int sickUserId, DateTime sickDate);
    }
}
