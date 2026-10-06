using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemPenjadwalanShiftGA.Models.Entities
{
    [Table("ScheduleDetails")]
    public class ScheduleDetail
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("SchedulePeriod")]
        public int SchedulePeriodId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("ShiftType")]
        public int? ShiftTypeId { get; set; } // Null = OFF / Libur

        public DateTime WorkDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Assigned"; // "Assigned", "Off", "Sick", "Replaced"

        public bool IsReplacement { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // Navigation
        public SchedulePeriod? SchedulePeriod { get; set; }
        public User? User { get; set; }
        public ShiftType? ShiftType { get; set; }
    }
}
