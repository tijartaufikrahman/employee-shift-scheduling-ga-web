using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemPenjadwalanShiftGA.Models.Entities
{
    [Table("LeaveRequests")]
    public class LeaveRequest
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(20)]
        public string LeaveType { get; set; } = "Cuti"; // "Cuti", "Sakit", "Izin"

        public DateTime RequestDate { get; set; }

        [Required]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"

        public string? RejectionNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // Navigation
        public User? User { get; set; }
        public ICollection<LeaveRequestAttachment> Attachments { get; set; } = new List<LeaveRequestAttachment>();
    }
}
