using SistemPenjadwalanShiftGA.Models.Entities;
using SistemPenjadwalanShiftGA.Models.ViewModels;

namespace SistemPenjadwalanShiftGA.Interfaces
{
    public interface IAuthService
    {
        Task<User?> LoginAsync(string email, string password);
        Task<bool> ForgotPasswordAsync(string email, string resetBaseUrl);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        Task<bool> ValidateResetTokenAsync(string token);
    }
}
