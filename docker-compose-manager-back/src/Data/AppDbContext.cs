using Microsoft.EntityFrameworkCore;
using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Session> Sessions { get; set; } = null!;
    public DbSet<ComposePath> ComposePaths { get; set; } = null!;
    public DbSet<ComposeFile> ComposeFiles { get; set; } = null!;
    public DbSet<AppSetting> AppSettings { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<Operation> Operations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();
            // Configure Role relationship instead of trying to map the Role object as a scalar
            entity.Property(e => e.RoleId).IsRequired();
            entity.HasOne(e => e.Role)
                .WithMany()
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Role configuration
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Permissions).IsRequired();
        });

        // Session configuration
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RefreshToken).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ComposePath configuration
        modelBuilder.Entity<ComposePath>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Path).IsUnique();
            entity.Property(e => e.Path).IsRequired();
        });

        // ComposeFile configuration
        modelBuilder.Entity<ComposeFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FullPath).IsUnique();
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.FullPath).IsRequired();
            entity.HasOne(e => e.ComposePath)
                .WithMany(cp => cp.ComposeFiles)
                .HasForeignKey(e => e.ComposePathId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AppSetting configuration
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Value).IsRequired();
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Action);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Operation configuration
        modelBuilder.Entity<Operation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OperationId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);
            entity.Property(e => e.OperationId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Use a fixed date for seeding to avoid non-deterministic model changes
        DateTime seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Seed default roles
        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = 1,
                Name = "admin",
                Permissions = "[\"all\"]",
                Description = "Administrator with full access",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = 2,
                Name = "user",
                Permissions = "[\"containers:read\",\"containers:write\",\"compose:read\"]",
                Description = "Regular user with limited access",
                CreatedAt = seedDate
            }
        );

        // Seed default admin user (password: admin)
        // BCrypt hash for "admin" with cost factor 12
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = "$2a$12$EaqK3eU12rvE2Pcx9EQpYuuBXguVhP48P8lPq.lcbDCAXTIRY9IdK",
                RoleId = 1, // admin role
                IsEnabled = true,
                MustChangePassword = true,
                CreatedAt = seedDate
            }
        );

        // Seed default compose path
        modelBuilder.Entity<ComposePath>().HasData(
            new ComposePath
            {
                Id = 1,
                Path = "/compose-files",
                IsReadOnly = false,
                IsEnabled = true,
                CreatedAt = seedDate
            }
        );
    }
}
