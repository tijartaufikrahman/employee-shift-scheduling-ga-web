using System.ComponentModel.DataAnnotations;

namespace SistemPenjadwalanShiftGA.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email harus diisi")]
        [EmailAddress(ErrorMessage = "Format email tidak valid")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password harus diisi")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = false;
    }
}
