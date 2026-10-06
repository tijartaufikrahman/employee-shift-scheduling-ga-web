using Microsoft.EntityFrameworkCore;
using SistemPenjadwalanShiftGA.Data;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Repositories
{
    public class ScheduleRepository : IScheduleRepository
    {
        private readonly AppDbContext _context;
        public ScheduleRepository(AppDbContext context) { _context = context; }

        public async Task<IEnumerable<SchedulePeriod>> GetAllPeriodsAsync()
        {
            return await _context.SchedulePeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
        }

        public async Task<SchedulePeriod?> GetPeriodByIdAsync(int id)
        {
            return await _context.SchedulePeriods
                .Include(p => p.ScheduleDetails)
                    .ThenInclude(d => d.User)
                        .ThenInclude(u => u.Role)
                .Include(p => p.ScheduleDetails)
                    .ThenInclude(d => d.ShiftType)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<ScheduleDetail>> GetDetailsByPeriodIdAsync(int periodId)
        {
            return await _context.ScheduleDetails
                .Include(d => d.User)
                    .ThenInclude(u => u.Role)
                .Include(d => d.ShiftType)
                .Where(d => d.SchedulePeriodId == periodId)
                .OrderBy(d => d.WorkDate)
                .ThenBy(d => d.User.NamaLengkap)
                .ToListAsync();
        }

        public async Task<ScheduleDetail?> GetDetailByIdAsync(int detailId)
        {
            return await _context.ScheduleDetails.FindAsync(detailId);
        }

        public async Task<IEnumerable<ScheduleDetail>> GetLastPeriodDetailsAsync()
        {
            var lastPeriod = await _context.SchedulePeriods
                .Where(p => p.IsPublished)
                .OrderByDescending(p => p.EndDate)
                .FirstOrDefaultAsync()
                ?? await _context.SchedulePeriods
                    .OrderByDescending(p => p.EndDate)
                    .FirstOrDefaultAsync();

            if (lastPeriod == null)
            {
                return Enumerable.Empty<ScheduleDetail>();
            }

            return await GetDetailsByPeriodIdAsync(lastPeriod.Id);
        }

        public async Task AddPeriodAsync(SchedulePeriod period)
        {
            await _context.SchedulePeriods.AddAsync(period);
        }

        public async Task UpdatePeriodAsync(SchedulePeriod period)
        {
            _context.SchedulePeriods.Update(period);
            await Task.CompletedTask;
        }

        public async Task DeletePeriodAsync(int id)
        {
            var details = _context.ScheduleDetails.Where(d => d.SchedulePeriodId == id);
            _context.ScheduleDetails.RemoveRange(details);

            var period = await _context.SchedulePeriods.FindAsync(id);
            if (period != null)
            {
                _context.SchedulePeriods.Remove(period);
            }
        }

        public async Task AddDetailAsync(ScheduleDetail detail)
        {
            await _context.ScheduleDetails.AddAsync(detail);
        }

        public async Task AddDetailsRangeAsync(IEnumerable<ScheduleDetail> details)
        {
            await _context.ScheduleDetails.AddRangeAsync(details);
        }

        public async Task UpdateDetailAsync(ScheduleDetail detail)
        {
            _context.ScheduleDetails.Update(detail);
            await Task.CompletedTask;
        }

        public async Task DeleteDetailsByPeriodIdAsync(int periodId)
        {
            var details = await _context.ScheduleDetails
                .Where(d => d.SchedulePeriodId == periodId)
                .ToListAsync();
            
            _context.ScheduleDetails.RemoveRange(details);
        }

        public async Task<IEnumerable<ScheduleDetail>> GetUserScheduleAsync(int userId, int periodId)
        {
            return await _context.ScheduleDetails
                .Include(d => d.ShiftType)
                .Where(d => d.UserId == userId && d.SchedulePeriodId == periodId)
                .OrderBy(d => d.WorkDate)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
