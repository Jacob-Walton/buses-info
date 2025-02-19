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
        public DbSet<ApiKey> ApiKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder? modelBuilder)
        {
            modelBuilder?.Entity<ApplicationUser>(entity =>
                    {
                        entity.HasIndex(u => u.Email).IsUnique();
                        // Add index for preferences columns
                        entity.HasIndex(u => new
                        {
                            u.PreferredRoutes,
                            u.ShowPreferredRoutesFirst,
                            u.EnableEmailNotifications
                        }).HasDatabaseName("IX_User_Preferences");

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

            modelBuilder?.Entity<ApiKeyRequest>(entity =>
            {
                entity.HasOne(r => r.User)
                    .WithMany()
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Add index for UserId foreign key
                entity.HasIndex(r => r.UserId);
                // Add index for common query patterns
                entity.HasIndex(r => new { r.Status, r.RequestedAt });
            });

            modelBuilder?.Entity<AdminSettings>(entity =>
                {
                    entity.HasKey(e => e.LastModified);
                    entity.Property(e => e.ApiRateLimit).IsRequired();
                    entity.Property(e => e.ApiKeyExpirationDays).IsRequired();
                    entity.Property(e => e.ArchivedDataRetentionDays).IsRequired();
                    entity.Property(e => e.MaintenanceWindow).IsRequired();
                    entity.Property(e => e.ModifiedBy).IsRequired();

                    // Add index for common query pattern
                    entity.HasIndex(e => e.ModifiedBy);
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

                    // Add comprehensive index for common query patterns
                    entity.HasIndex(e => new { e.Service, e.Status, e.ArrivalTime })
                        .HasDatabaseName("IX_BusArrival_ServiceStatusTime");
                    // Add index for weather-based queries
                    entity.HasIndex(e => new { e.Weather, e.ArrivalTime });
                });

            modelBuilder?.Entity<ApiKey>(entity =>
            {
                entity.HasKey(e => e.Key);
                entity.Property(e => e.Key).HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Add index for user lookups
                entity.HasIndex(e => e.UserId);
            });
        }
    }
}