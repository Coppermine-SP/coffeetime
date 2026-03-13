using coffeetime.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace coffeetime.Contexts
{
    public class ServerDbContext(DbContextOptions<ServerDbContext> options) : DbContext(options)
    {
        public DbSet<Item> Items { get; set; }
        public DbSet<PackageBatch> Batches { get; set; }
        public DbSet<BatchTake> BatchTakes { get; set; }
        public DbSet <UserCache> UserCaches { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var utcDateTimeOffsetConverter = new ValueConverter<DateTimeOffset, DateTime>(
                v => v.UtcDateTime,
                v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc), TimeSpan.Zero));

            modelBuilder.Entity<Item>(entity =>
            {
                entity.ToTable("items");

                entity.HasKey(e => e.ItemId);

                entity.Property(e => e.ItemId)
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.ItemName)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.ItemDescription)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.ItemPrice)
                    .HasColumnType("int unsigned")
                    .IsRequired();
            });

            modelBuilder.Entity<UserCache>(entity =>
            {
                entity.ToTable("user_cache");

                entity.HasKey(e => e.UserObjectGuid);

                entity.Property(e => e.UserObjectGuid)
                    .HasMaxLength(36)
                    .IsRequired();

                entity.Property(e => e.UserDisplayName)
                    .HasMaxLength(30)
                    .IsRequired();
            });

            modelBuilder.Entity<PackageBatch>(entity =>
            {
                entity.ToTable("package_batches", tb =>
                {
                    tb.HasCheckConstraint(
                        "CK_pkg_batch_count",
                        "`BatchCount` BETWEEN 1 AND 30");

                    tb.HasCheckConstraint(
                        "CK_pkg_remaining_count",
                        "`RemainingCount` BETWEEN 0 AND 30");
                });

                entity.HasKey(e => e.BatchId);

                entity.Property(e => e.BatchId)
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.ItemId)
                    .IsRequired();

                entity.Property(e => e.OwnerUserId)
                    .HasMaxLength(36)
                    .IsRequired();

                entity.Property(e => e.RoastedAtUtc)
                    .HasConversion(utcDateTimeOffsetConverter)
                    .HasColumnType("datetime(6)")
                    .IsRequired();

                entity.Property(e => e.BatchCount)
                    .IsRequired();

                entity.Property(e => e.RemainingCount)
                    .IsRequired();

                entity.HasOne(e => e.Item)
                    .WithMany(i => i.Batches)
                    .HasForeignKey(e => e.ItemId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_pkg_batch_item");

                entity.HasOne(e => e.OwnerUser)
                    .WithMany(u => u.OwnedBatches)
                    .HasForeignKey(e => e.OwnerUserId)
                    .HasPrincipalKey(u => u.UserObjectGuid)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_pkg_batch_owner");

                entity.HasIndex(e => new { e.RemainingCount, e.RoastedAtUtc })
                    .HasDatabaseName("IX_pkg_remaining_roasted");

                entity.HasIndex(e => new { e.ItemId, e.RoastedAtUtc })
                    .HasDatabaseName("IX_pkg_item_roasted");

                entity.HasIndex(e => new { e.OwnerUserId, e.RemainingCount })
                    .HasDatabaseName("IX_pkg_owner_remaining");
            });

            modelBuilder.Entity<BatchTake>(entity =>
            {
                entity.ToTable("batch_takes", tb =>
                {
                    tb.HasCheckConstraint(
                        "CK_batch_take_qty",
                        "`Quantity` BETWEEN 1 AND 10");
                });

                entity.HasKey(e => e.BatchTakeId);

                entity.Property(e => e.BatchTakeId)
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.BatchId)
                    .IsRequired();

                entity.Property(e => e.TakenByUserId)
                    .HasMaxLength(36)
                    .IsRequired();

                entity.Property(e => e.Quantity)
                    .IsRequired();

                entity.Property(e => e.CreatedAtUtc)
                    .HasConversion(utcDateTimeOffsetConverter)
                    .HasColumnType("datetime(6)")
                    .IsRequired();

                entity.HasOne(e => e.Batch)
                    .WithMany(b => b.Takes)
                    .HasForeignKey(e => e.BatchId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_batch_take_batch");

                entity.HasOne(e => e.TakenByUser)
                    .WithMany(u => u.TakenBatches)
                    .HasForeignKey(e => e.TakenByUserId)
                    .HasPrincipalKey(u => u.UserObjectGuid)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_batch_take_user");

                entity.HasIndex(e => new { e.BatchId, e.CreatedAtUtc })
                    .HasDatabaseName("IX_take_batch_created");

                entity.HasIndex(e => new { e.CreatedAtUtc})
                    .HasDatabaseName("IX_take_created");
            });
        }
    }
}
