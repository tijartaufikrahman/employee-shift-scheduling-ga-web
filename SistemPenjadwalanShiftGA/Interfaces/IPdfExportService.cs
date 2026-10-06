using System.Threading.Tasks;

namespace SistemPenjadwalanShiftGA.Interfaces
{
    public interface IPdfExportService
    {
        Task<(byte[] PdfBytes, string QrVerificationCode)> GenerateSchedulePdfAsync(int periodId, string managerName);
    }
}
