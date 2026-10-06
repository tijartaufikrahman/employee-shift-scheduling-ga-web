using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Interfaces
{
    public interface IScheduleRepository
    {
        Task<IEnumerable<SchedulePeriod>> GetAllPeriodsAsync();
        Task<SchedulePeriod?> GetPeriodByIdAsync(int id);
        Task<IEnumerable<ScheduleDetail>> GetDetailsByPeriodIdAsync(int periodId);
        Task<ScheduleDetail?> GetDetailByIdAsync(int detailId);
        Task<IEnumerable<ScheduleDetail>> GetLastPeriodDetailsAsync();
        Task AddPeriodAsync(SchedulePeriod period);
        Task UpdatePeriodAsync(SchedulePeriod period);
        Task DeletePeriodAsync(int id);
        Task AddDetailAsync(ScheduleDetail detail);
        Task AddDetailsRangeAsync(IEnumerable<ScheduleDetail> details);
        Task UpdateDetailAsync(ScheduleDetail detail);
        Task DeleteDetailsByPeriodIdAsync(int periodId);
        Task<IEnumerable<ScheduleDetail>> GetUserScheduleAsync(int userId, int periodId);
        Task SaveChangesAsync();
    }
}
