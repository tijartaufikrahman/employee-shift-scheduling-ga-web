using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Interfaces
{
    public interface IShiftTypeRepository
    {
        Task<IEnumerable<ShiftType>> GetAllAsync();
        Task<ShiftType?> GetByIdAsync(int id);
        Task AddAsync(ShiftType shiftType);
        Task UpdateAsync(ShiftType shiftType);
        Task DeleteAsync(int id);
        Task SaveChangesAsync();
    }
}
