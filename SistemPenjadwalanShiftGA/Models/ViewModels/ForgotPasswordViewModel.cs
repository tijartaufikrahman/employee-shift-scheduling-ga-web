using System.ComponentModel.DataAnnotations;

namespace SistemPenjadwalanShiftGA.Models.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email harus diisi")]
        [EmailAddress(ErrorMessage = "Format email tidak valid")]
        public string Email { get; set; } = string.Empty;
    }
}
