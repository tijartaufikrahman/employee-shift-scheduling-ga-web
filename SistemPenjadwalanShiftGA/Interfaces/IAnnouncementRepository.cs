using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Interfaces
{
    public interface IAnnouncementRepository
    {
        Task<IEnumerable<Announcement>> GetAllAsync();
        Task<Announcement?> GetByIdAsync(int id);
        Task AddAsync(Announcement announcement);
        Task UpdateAsync(Announcement announcement);
        Task DeleteAsync(int id);
        Task SaveChangesAsync();
    }
}
