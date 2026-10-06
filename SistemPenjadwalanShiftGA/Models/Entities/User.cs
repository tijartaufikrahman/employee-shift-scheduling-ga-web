using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemPenjadwalanShiftGA.Models.Entities
{
    [Table("Users")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Role")]
        public int RoleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string NamaLengkap { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        [MaxLength(20)]
        public string Level { get; set; } = "Junior"; // "Senior" atau "Junior"

        public bool IsActive { get; set; } = true;

        [MaxLength(255)]
        public string? ResetPasswordToken { get; set; }

        public DateTime? TokenCreatedAt { get; set; }

        public DateTime? TokenExpiredAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // Navigation
        public Role? Role { get; set; }
        public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
        public ICollection<ScheduleDetail> ScheduleDetails { get; set; } = new List<ScheduleDetail>();
    }
}
