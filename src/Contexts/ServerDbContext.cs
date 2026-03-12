using Microsoft.EntityFrameworkCore;
using coffeetime.Models;

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
            modelBuilder.Entity<PackageBatch>(b =>
            {
                b.HasOne(x => x.Item)
                    .WithMany(x => x.Batches)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.OwnerUser)
                    .WithMany(x => x.OwnedBatches)
                    .HasForeignKey(x => x.OwnerUserId)
                    .HasPrincipalKey(x => x.UserObjectGuid)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => new { x.OwnerUserId, x.RemainingCount });
                b.HasIndex(x => new { x.ItemId, x.RoastedAt });
            });

            modelBuilder.Entity<BatchTake>(t =>
            {
                t.HasOne(x => x.Batch)
                    .WithMany(x => x.Takes)
                    .HasForeignKey(x => x.BatchId)
                    .OnDelete(DeleteBehavior.Restrict);

                t.HasOne(x => x.TakenByUser)
                    .WithMany(x => x.TakenBatches)
                    .HasForeignKey(x => x.TakenByUserId)
                    .HasPrincipalKey(x => x.UserObjectGuid)
                    .OnDelete(DeleteBehavior.Restrict);

                t.HasIndex(x => new { x.BatchId, x.CreatedAt });
                t.HasIndex(x => new { x.TakenByUserId, x.CreatedAt });
            });
        }
    }
}
