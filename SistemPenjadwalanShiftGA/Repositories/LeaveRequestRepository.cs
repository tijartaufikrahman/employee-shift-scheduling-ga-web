using Microsoft.EntityFrameworkCore;
using SistemPenjadwalanShiftGA.Data;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Repositories
{
    public class LeaveRequestRepository : ILeaveRequestRepository
    {
        private readonly AppDbContext _context;
        public LeaveRequestRepository(AppDbContext context) { _context = context; }

        public async Task<IEnumerable<LeaveRequest>> GetAllAsync()
        {
            return await _context.LeaveRequests
                .Include(l => l.User)
                .Include(l => l.Attachments)
                .ToListAsync();
        }

        public async Task<LeaveRequest?> GetByIdAsync(int id)
        {
            return await _context.LeaveRequests
                .Include(l => l.User)
                .Include(l => l.Attachments)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<IEnumerable<LeaveRequest>> GetByUserIdAsync(int userId)
        {
            return await _context.LeaveRequests
                .Include(l => l.Attachments)
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<LeaveRequest>> GetPendingAsync()
        {
            return await _context.LeaveRequests
                .Include(l => l.User)
                .Include(l => l.Attachments)
                .Where(l => l.Status == "Pending")
                .ToListAsync();
        }

        public async Task<IEnumerable<LeaveRequest>> GetApprovedForDateRangeAsync(DateTime start, DateTime end)
        {
            var startDate = start.Date;
            var endDate = end.Date.AddDays(1).AddTicks(-1);
            return await _context.LeaveRequests
                .Where(l => l.Status == "Approved" && l.RequestDate >= startDate && l.RequestDate <= endDate)
                .ToListAsync();
        }

        public async Task AddAsync(LeaveRequest leaveRequest)
        {
            await _context.LeaveRequests.AddAsync(leaveRequest);
        }

        public async Task UpdateAsync(LeaveRequest leaveRequest)
        {
            _context.LeaveRequests.Update(leaveRequest);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(int id)
        {
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest != null)
            {
                _context.LeaveRequests.Remove(leaveRequest);
            }
        }

        public async Task AddAttachmentAsync(LeaveRequestAttachment attachment)
        {
            await _context.LeaveRequestAttachments.AddAsync(attachment);
        }

        public async Task DeleteAttachmentAsync(int attachmentId)
        {
            var attachment = await _context.LeaveRequestAttachments.FindAsync(attachmentId);
            if (attachment != null)
            {
                _context.LeaveRequestAttachments.Remove(attachment);
            }
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
