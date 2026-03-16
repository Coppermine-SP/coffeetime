using coffeetime.Contexts;
using coffeetime.Models;
using Microsoft.EntityFrameworkCore;

namespace coffeetime.Services
{
    public class BatchService(IDbContextFactory<ServerDbContext> factory, ILogger<ItemService> logger)
    {
        public async Task AddBatchAsync(PackageBatch batch)
        {
            using var context = await factory.CreateDbContextAsync();
            await context.Batches.AddAsync(batch);
            await context.SaveChangesAsync();
            logger.LogInformation($"Added batch #{batch.BatchId} to the database.");
        }
     

    public async Task AddBatchTakeAsync(BatchTake take, CancellationToken cancellationToken = default)
    {
   
        if (take.Quantity is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(take.Quantity), "수량은 1~100 사이여야 합니다.");

        if (string.IsNullOrWhiteSpace(take.TakenByUserId))
            throw new ArgumentException("사용자 ID는 필수입니다.", nameof(take.TakenByUserId));

        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var affectedRows = await context.Batches
                .Where(b => b.BatchId == take.BatchId && b.RemainingCount >= take.Quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        b => b.RemainingCount,
                        b => b.RemainingCount - take.Quantity),
                    cancellationToken);

            if (affectedRows == 0)
            {
                var batchExists = await context.Batches
                    .AnyAsync(b => b.BatchId == take.BatchId, cancellationToken);

                throw batchExists
                    ? new InvalidOperationException("남은 수량이 부족하여 가져가기를 완료할 수 없습니다.")
                    : new KeyNotFoundException($"배치 #{take.BatchId}를 찾을 수 없습니다.");
            }

            var entity = new BatchTake
            {
                BatchId = take.BatchId,
                TakenByUserId = take.TakenByUserId,
                Quantity = take.Quantity,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            await context.BatchTakes.AddAsync(entity, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Added batch take #{BatchTakeId}. Batch #{BatchId}, user {UserId}, quantity {Quantity}.",
                entity.BatchTakeId,
                entity.BatchId,
                entity.TakenByUserId,
                entity.Quantity);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
}
