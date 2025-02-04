using Microsoft.EntityFrameworkCore;
using BusInfo.Models;
using System;

namespace BusInfo.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<BusStatus> BusStatuses { get; set; }
        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<ApiKeyRequest> ApiKeyRequests { get; set; }
        public DbSet<AdminSettings> AdminSettings { get; set; }
        public DbSet<BusArrival> BusArrivals { get; set; }

        protected override void OnModelCreating(ModelBuilder? modelBuilder)
        {
            modelBuilder?.Entity<ApplicationUser>(entity =>
                    {
                        entity.HasIndex(u => u.Email).IsUnique();
                        entity.HasIndex(u => u.ApiKey).IsUnique();

                        // Basic columns
                        entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
                        entity.Property(u => u.PasswordHash).IsRequired();
                        entity.Property(u => u.Salt).IsRequired();

                        // Array/List properties
                        entity.Property(u => u.PreferredRoutes)
                            .HasColumnType("text[]");
                        entity.Property(u => u.RecoveryCodes)
                            .HasColumnType("text[]");

                        // Nullable columns with specific types
                        entity.Property(u => u.ApiKey)
                            .HasColumnType("varchar(100)");
                        entity.Property(u => u.PasswordResetToken)
                            .HasColumnType("varchar(100)");
                        entity.Property(u => u.EmailVerificationToken)
                            .HasColumnType("varchar(100)");
                        entity.Property(u => u.TwoFactorSecret)
                            .HasColumnType("varchar(100)");

                        // DateTime columns
                        entity.Property(u => u.CreatedAt)
                            .HasColumnType("timestamp with time zone");
                        entity.Property(u => u.LastLoginAt)
                            .HasColumnType("timestamp with time zone");
                        entity.Property(u => u.PasswordResetTokenExpiry)
                            .HasColumnType("timestamp with time zone");
                        entity.Property(u => u.EmailVerificationTokenExpiry)
                            .HasColumnType("timestamp with time zone");
                        entity.Property(u => u.LastPasswordChangeDate)
                            .HasColumnType("timestamp with time zone");
                        entity.Property(u => u.LockoutEnd)
                            .HasColumnType("timestamp with time zone");
                    });

            modelBuilder?.Entity<ApiKeyRequest>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder?.Entity<AdminSettings>(entity =>
                {
                    entity.HasKey(e => e.LastModified);
                    entity.Property(e => e.ApiRateLimit).IsRequired();
                    entity.Property(e => e.ApiKeyExpirationDays).IsRequired();
                    entity.Property(e => e.ArchivedDataRetentionDays).IsRequired();
                    entity.Property(e => e.MaintenanceWindow).IsRequired();
                    entity.Property(e => e.ModifiedBy).IsRequired();
                });

            modelBuilder?.Entity<BusArrival>(entity =>
                {
                    entity.HasKey(e => e.Id);
                    entity.Property(e => e.Service).IsRequired().HasMaxLength(10);
                    entity.Property(e => e.Bay).IsRequired().HasMaxLength(10);
                    entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                    entity.Property(e => e.Weather).HasMaxLength(50);
                    entity.Property(e => e.ArrivalTime).HasColumnType("timestamp with time zone");

                    entity.HasIndex(e => new { e.Service, e.ArrivalTime });
                });
        }
    }
}