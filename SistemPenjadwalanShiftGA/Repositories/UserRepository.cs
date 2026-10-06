using Microsoft.EntityFrameworkCore;
using SistemPenjadwalanShiftGA.Data;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByEmailAsync(string email)
            => await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

        public async Task<User?> GetByIdAsync(int id)
            => await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

        public async Task<User?> GetByResetTokenAsync(string token)
            => await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.ResetPasswordToken == token);

        public async Task<IEnumerable<User>> GetAllAsync()
            => await _context.Users.Include(u => u.Role).ToListAsync();

        public async Task AddAsync(User user)
            => await _context.Users.AddAsync(user);

        public Task UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null) _context.Users.Remove(user);
        }

        public async Task SaveChangesAsync()
            => await _context.SaveChangesAsync();
    }
}
