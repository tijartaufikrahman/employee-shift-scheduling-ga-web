using Microsoft.EntityFrameworkCore;
using SistemPenjadwalanShiftGA.Data;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Repositories
{
    public class ShiftTypeRepository : IShiftTypeRepository
    {
        private readonly AppDbContext _context;
        public ShiftTypeRepository(AppDbContext context) { _context = context; }

        public async Task<IEnumerable<ShiftType>> GetAllAsync()
        {
            return await _context.ShiftTypes.ToListAsync();
        }

        public async Task<ShiftType?> GetByIdAsync(int id)
        {
            return await _context.ShiftTypes.FindAsync(id);
        }

        public async Task AddAsync(ShiftType shiftType)
        {
            await _context.ShiftTypes.AddAsync(shiftType);
        }

        public async Task UpdateAsync(ShiftType shiftType)
        {
            _context.ShiftTypes.Update(shiftType);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(int id)
        {
            var shiftType = await _context.ShiftTypes.FindAsync(id);
            if (shiftType != null)
            {
                _context.ShiftTypes.Remove(shiftType);
            }
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
