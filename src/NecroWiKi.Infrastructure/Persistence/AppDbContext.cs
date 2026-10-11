using Microsoft.EntityFrameworkCore;

namespace NecroWiKi.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet<SaveStateUser> SaveStateUsers => Set<SaveStateUser>();
        public DbSet<PortalAccount> PortalAccounts => Set<PortalAccount>();

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
            modelBuilder.Entity<PortalAccount>(entity =>
            {
                entity.ToTable("PORTAL_ACCOUNTS");
                entity.HasKey(account => account.Id);
                entity.Property(account => account.Id).HasColumnName("ID").HasColumnType("char(36)");
                entity.Property(account => account.UserName).HasColumnName("USERNAME").HasMaxLength(32).IsRequired();
                entity.Property(account => account.NormalizedUserName).HasColumnName("NORMALIZED_USERNAME").HasMaxLength(32).IsRequired();
                entity.Property(account => account.PasswordHash).HasColumnName("PASSWORD_HASH").HasMaxLength(255).IsRequired();
                entity.Property(account => account.Role).HasColumnName("ROLE").HasMaxLength(16).IsRequired();
                entity.Property(account => account.IsActive).HasColumnName("IS_ACTIVE").IsRequired();
                entity.Property(account => account.CreatedAtUtc).HasColumnName("CREATED_AT_UTC").IsRequired();
                entity.HasIndex(account => account.NormalizedUserName).IsUnique();
            });
        }


    }
}
