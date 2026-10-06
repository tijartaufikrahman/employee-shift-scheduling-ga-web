namespace SistemPenjadwalanShiftGA.Interfaces
{
    public interface IEmailService
    {
        Task SendResetPasswordEmailAsync(string toEmail, string toName, string resetLink);
    }
}
