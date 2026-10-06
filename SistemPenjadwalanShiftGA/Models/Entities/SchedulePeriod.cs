using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemPenjadwalanShiftGA.Models.Entities
{
    [Table("SchedulePeriods")]
    public class SchedulePeriod
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ScheduleCode { get; set; } = string.Empty;

        private string _periodName = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PeriodName
        {
            get => System.Text.RegularExpressions.Regex.Replace(_periodName ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            set => _periodName = System.Text.RegularExpressions.Regex.Replace(value ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string DurationType { get; set; } = "1_Week"; // "1_Week" atau "2_Weeks"

        public bool IsPublished { get; set; } = false;

        // Dynamic Scheduling Policies
        public int MaxConsecutiveDays { get; set; } = 5;
        public int MaxOffDays { get; set; } = 2;
        public bool PreventNightToMorning { get; set; } = true;
        public int MinSenior { get; set; } = 1;
        public int? NightShiftId { get; set; }

        [ForeignKey("NightShiftId")]
        public ShiftType? NightShift { get; set; }

        [MaxLength(500)]
        public string? PdfFilePath { get; set; }

        [MaxLength(500)]
        public string? QrCodePath { get; set; }

        [MaxLength(100)]
        public string? QrVerificationCode { get; set; } = Guid.NewGuid().ToString("N");

        public DateTime? GeneratedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // Navigation
        public ICollection<ScheduleDetail> ScheduleDetails { get; set; } = new List<ScheduleDetail>();
    }
}
