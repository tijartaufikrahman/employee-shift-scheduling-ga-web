using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Interfaces
{
    public interface ILeaveRequestRepository
    {
        Task<IEnumerable<LeaveRequest>> GetAllAsync();
        Task<LeaveRequest?> GetByIdAsync(int id);
        Task<IEnumerable<LeaveRequest>> GetByUserIdAsync(int userId);
        Task<IEnumerable<LeaveRequest>> GetPendingAsync();
        Task<IEnumerable<LeaveRequest>> GetApprovedForDateRangeAsync(DateTime start, DateTime end);
        Task AddAsync(LeaveRequest leaveRequest);
        Task UpdateAsync(LeaveRequest leaveRequest);
        Task DeleteAsync(int id);
        Task AddAttachmentAsync(LeaveRequestAttachment attachment);
        Task DeleteAttachmentAsync(int attachmentId);
        Task SaveChangesAsync();
    }
}
