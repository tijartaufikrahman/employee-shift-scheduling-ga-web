using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemPenjadwalanShiftGA.Models.Entities
{
    [Table("LeaveRequestAttachments")]
    public class LeaveRequestAttachment
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("LeaveRequest")]
        public int LeaveRequestId { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string FileType { get; set; } = string.Empty;

        public int FileSize { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // Navigation
        public LeaveRequest? LeaveRequest { get; set; }
    }
}
