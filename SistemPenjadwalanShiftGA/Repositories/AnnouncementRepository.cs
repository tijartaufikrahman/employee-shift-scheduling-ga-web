using Microsoft.EntityFrameworkCore;
using SistemPenjadwalanShiftGA.Data;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Repositories
{
    public class AnnouncementRepository : IAnnouncementRepository
    {
        private readonly AppDbContext _context;
        public AnnouncementRepository(AppDbContext context) { _context = context; }

        public async Task<IEnumerable<Announcement>> GetAllAsync()
        {
            return await _context.Announcements
                .Include(a => a.User)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<Announcement?> GetByIdAsync(int id)
        {
            return await _context.Announcements
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task AddAsync(Announcement announcement)
        {
            await _context.Announcements.AddAsync(announcement);
        }

        public async Task UpdateAsync(Announcement announcement)
        {
            _context.Announcements.Update(announcement);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(int id)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement != null)
            {
                _context.Announcements.Remove(announcement);
            }
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
