using Microsoft.EntityFrameworkCore;
using SistemPenjadwalanShiftGA.Data;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly AppDbContext _context;

        public RoleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Role>> GetAllAsync()
            => await _context.Roles.OrderBy(r => r.NamaRole).ToListAsync();

        public async Task<Role?> GetByIdAsync(int id)
            => await _context.Roles.FindAsync(id);

        public async Task AddAsync(Role role)
            => await _context.Roles.AddAsync(role);

        public Task UpdateAsync(Role role)
        {
            _context.Roles.Update(role);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role != null) _context.Roles.Remove(role);
        }

        public async Task<bool> HasUsersAsync(int roleId)
            => await _context.Users.AnyAsync(u => u.RoleId == roleId);

        public async Task SaveChangesAsync()
            => await _context.SaveChangesAsync();
    }
}
