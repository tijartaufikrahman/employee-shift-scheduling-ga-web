using Microsoft.EntityFrameworkCore;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<ShiftType> ShiftTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveRequestAttachment> LeaveRequestAttachments { get; set; }
        public DbSet<SchedulePeriod> SchedulePeriods { get; set; }
        public DbSet<ScheduleDetail> ScheduleDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relasi Users -> Roles
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relasi ScheduleDetail
            modelBuilder.Entity<ScheduleDetail>()
                .HasOne(sd => sd.SchedulePeriod)
                .WithMany(sp => sp.ScheduleDetails)
                .HasForeignKey(sd => sd.SchedulePeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScheduleDetail>()
                .HasOne(sd => sd.User)
                .WithMany(u => u.ScheduleDetails)
                .HasForeignKey(sd => sd.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ScheduleDetail>()
                .HasOne(sd => sd.ShiftType)
                .WithMany(st => st.ScheduleDetails)
                .HasForeignKey(sd => sd.ShiftTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Relasi LeaveRequestAttachment
            modelBuilder.Entity<LeaveRequestAttachment>()
                .HasOne(lra => lra.LeaveRequest)
                .WithMany(lr => lr.Attachments)
                .HasForeignKey(lra => lra.LeaveRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
