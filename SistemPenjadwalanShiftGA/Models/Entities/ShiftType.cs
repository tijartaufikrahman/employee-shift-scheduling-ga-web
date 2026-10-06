using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemPenjadwalanShiftGA.Models.Entities
{
    [Table("ShiftTypes")]
    public class ShiftType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ShiftName { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string ShiftCode { get; set; } = string.Empty;

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public int MinStaff { get; set; } = 3;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // Navigation
        public ICollection<ScheduleDetail> ScheduleDetails { get; set; } = new List<ScheduleDetail>();
    }
}
