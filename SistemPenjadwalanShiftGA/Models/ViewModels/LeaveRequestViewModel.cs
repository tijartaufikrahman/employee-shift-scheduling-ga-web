using System.ComponentModel.DataAnnotations;

namespace SistemPenjadwalanShiftGA.Models.ViewModels
{
    public class LeaveRequestCreateViewModel
    {
        [Required(ErrorMessage = "Jenis izin wajib dipilih.")]
        public string LeaveType { get; set; } = "Cuti"; // "Cuti", "Sakit", "Izin"

        [Required(ErrorMessage = "Tanggal permohonan wajib diisi.")]
        [DataType(DataType.Date)]
        public DateTime RequestDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Alasan permohonan wajib diisi.")]
        [MinLength(3, ErrorMessage = "Alasan permohonan minimal 3 karakter.")]
        public string Reason { get; set; } = string.Empty;

        public List<IFormFile>? Attachments { get; set; }
    }

    public class LeaveRequestEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Jenis izin wajib dipilih.")]
        public string LeaveType { get; set; } = "Cuti";

        [Required(ErrorMessage = "Tanggal permohonan wajib diisi.")]
        [DataType(DataType.Date)]
        public DateTime RequestDate { get; set; }

        [Required(ErrorMessage = "Alasan permohonan wajib diisi.")]
        [MinLength(3, ErrorMessage = "Alasan permohonan minimal 3 karakter.")]
        public string Reason { get; set; } = string.Empty;

        public List<SistemPenjadwalanShiftGA.Models.Entities.LeaveRequestAttachment> ExistingAttachments { get; set; } = new();

        public List<IFormFile>? NewAttachments { get; set; }
    }

    public class LeaveRequestReviewViewModel
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty; // "Approved" or "Rejected"
        public string? RejectionNote { get; set; }
    }

    public class LeaveRequestDetailViewModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string LeaveType { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionNote { get; set; }
        public ICollection<SistemPenjadwalanShiftGA.Models.Entities.LeaveRequestAttachment> Attachments { get; set; } = new List<SistemPenjadwalanShiftGA.Models.Entities.LeaveRequestAttachment>();
    }

    public class DashboardViewModel
    {
        public int TotalKaryawan { get; set; }

        public int TotalShiftTypes { get; set; }
        public int TotalShiftType { get => TotalShiftTypes; set => TotalShiftTypes = value; }

        public int TotalPeriods { get; set; }
        public int TotalPeriodeJadwal { get => TotalPeriods; set => TotalPeriods = value; }

        public int PendingLeaveRequests { get; set; }
        public int IzinPending { get => PendingLeaveRequests; set => PendingLeaveRequests = value; }

        public int PublishedPeriods { get; set; }
        public int PublishedJadwal { get => PublishedPeriods; set => PublishedPeriods = value; }

        public List<SistemPenjadwalanShiftGA.Models.Entities.Announcement> RecentAnnouncements { get; set; } = new();
        public SistemPenjadwalanShiftGA.Models.Entities.SchedulePeriod? ActivePeriod { get; set; }
        public List<SistemPenjadwalanShiftGA.Models.Entities.LeaveRequest> RecentLeaveRequests { get; set; } = new();
    }

    public class MyScheduleItem
    {
        public DateTime Date { get; set; }
        public string ShiftCode { get; set; } = "OFF";
        public bool IsSick { get; set; }
    }

    public class EmployeeDashboardViewModel
    {
        public string EmployeeName { get; set; } = string.Empty;
        public SistemPenjadwalanShiftGA.Models.Entities.SchedulePeriod? ActiveSchedulePeriod { get; set; }
        public List<MyScheduleItem> MySchedule { get; set; } = new();

        public List<SistemPenjadwalanShiftGA.Models.Entities.Announcement> Announcements { get; set; } = new();
        public List<SistemPenjadwalanShiftGA.Models.Entities.Announcement> RecentAnnouncements
        {
            get => Announcements;
            set => Announcements = value;
        }

        public List<SistemPenjadwalanShiftGA.Models.Entities.LeaveRequest> MyLeaveRequests { get; set; } = new();
        public List<SistemPenjadwalanShiftGA.Models.Entities.LeaveRequest> MyRecentLeaves
        {
            get => MyLeaveRequests;
            set => MyLeaveRequests = value;
        }

        public ScheduleMatrixViewModel? MyCurrentSchedule { get; set; }
    }
}
