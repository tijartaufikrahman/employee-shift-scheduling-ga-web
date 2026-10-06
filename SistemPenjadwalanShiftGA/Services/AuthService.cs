using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepo, IEmailService emailService, ILogger<AuthService> logger)
        {
            _userRepo = userRepo;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<User?> LoginAsync(string email, string password)
        {
            var user = await _userRepo.GetByEmailAsync(email);
            if (user == null) return null;
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;
            return user;
        }

        public async Task<bool> ForgotPasswordAsync(string email, string resetBaseUrl)
        {
            var user = await _userRepo.GetByEmailAsync(email);
            if (user == null) return false;

            var token = Guid.NewGuid().ToString("N");
            user.ResetPasswordToken = token;
            user.TokenExpiredAt = DateTime.UtcNow.AddHours(1);

            await _userRepo.UpdateAsync(user);
            await _userRepo.SaveChangesAsync();

            var resetLink = $"{resetBaseUrl}?token={token}";

            try
            {
                await _emailService.SendResetPasswordEmailAsync(user.Email, user.NamaLengkap, resetLink);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal mengirim email reset password");
                // Rollback token
                user.ResetPasswordToken = null;
                user.TokenExpiredAt = null;
                await _userRepo.UpdateAsync(user);
                await _userRepo.SaveChangesAsync();
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var user = await _userRepo.GetByResetTokenAsync(token);
            if (user == null) return false;
            if (user.TokenExpiredAt < DateTime.UtcNow) return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.ResetPasswordToken = null;
            user.TokenExpiredAt = null;

            await _userRepo.UpdateAsync(user);
            await _userRepo.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ValidateResetTokenAsync(string token)
        {
            var user = await _userRepo.GetByResetTokenAsync(token);
            if (user == null) return false;
            if (user.TokenExpiredAt < DateTime.UtcNow) return false;
            return true;
        }
    }
}
