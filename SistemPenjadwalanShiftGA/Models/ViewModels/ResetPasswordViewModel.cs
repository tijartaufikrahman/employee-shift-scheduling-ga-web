using System.ComponentModel.DataAnnotations;

namespace SistemPenjadwalanShiftGA.Models.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password baru harus diisi")]
        [MinLength(8, ErrorMessage = "Password minimal 8 karakter")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Konfirmasi password harus diisi")]
        [Compare("NewPassword", ErrorMessage = "Password tidak cocok")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
