using Microsoft.EntityFrameworkCore;

namespace NecroWiKi.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet<SaveStateUser> SaveStateUsers => Set<SaveStateUser>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            modelBuilder.Entity<SaveStateUser>(entity =>
            {
                entity.ToTable("SAVE_STATE_USERS");
                entity.HasKey(user => user.Uuid);
                entity.Property(user => user.Uuid).HasColumnName("UUID").HasColumnType("char(36)");
                entity.Property(user => user.User).HasColumnName("USER").HasMaxLength(64).IsRequired();
                entity.Property(user => user.PasswordHash).HasColumnName("PASSWORD").HasMaxLength(255).IsRequired();
                entity.HasIndex(user => user.User).IsUnique();
            });
        }


    }
}
