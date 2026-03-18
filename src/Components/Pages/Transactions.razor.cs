using coffeetime.Contexts;
using coffeetime.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace coffeetime.Components.Pages
{
    public partial class Transactions(IDbContextFactory<ServerDbContext> factory) : ComponentBase
    {
        private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);

        private readonly TransactionQueryModel filter = new();
        private readonly HashSet<int> expandedTransactionIds = [];

        private List<TransactionListRow> pagedTransactions = [];
        private Dictionary<int, List<BatchUserSummaryRow>> batchUserSummaryLookup = [];
        private Dictionary<int, List<BatchHistoryRow>> batchHistoryLookup = [];
        private List<UserOption> takeUserOptions = [];
        private List<UserOption> ownerOptions = [];

        private bool isLoading;
        private int totalCount;
        private int totalQuantity;
        private int uniqueUserCount;
        private int uniqueBatchCount;
        private decimal filteredTotalEstimatedAmount;

        private int TotalPages => Math.Max(1, (int)Math.Ceiling(totalCount / (double)filter.PageSize));
        private int StartRow => totalCount == 0 ? 0 : ((filter.Page - 1) * filter.PageSize) + 1;
        private int EndRow => totalCount == 0 ? 0 : Math.Min(filter.Page * filter.PageSize, totalCount);
        private decimal CurrentPageEstimatedAmount => pagedTransactions.Sum(x => x.EstimatedPayableAmount);

        protected override async Task OnInitializedAsync()
        {
            await LoadFilterOptionsAsync();
            await LoadAsync();
        }

        private async Task LoadFilterOptionsAsync()
        {
            using var context = await factory.CreateDbContextAsync();

            takeUserOptions = await context.BatchTakes
                .AsNoTracking()
                .GroupBy(x => new { x.TakenByUserId, x.TakenByUser.UserDisplayName })
                .Select(g => new UserOption
                {
                    UserId = g.Key.TakenByUserId,
                    UserDisplayName = g.Key.UserDisplayName
                })
                .OrderBy(x => x.UserDisplayName)
                .ToListAsync();

            ownerOptions = await context.Batches
                .AsNoTracking()
                .GroupBy(x => new { x.OwnerUserId, x.OwnerUser.UserDisplayName })
                .Select(g => new UserOption
                {
                    UserId = g.Key.OwnerUserId,
                    UserDisplayName = g.Key.UserDisplayName
                })
                .OrderBy(x => x.UserDisplayName)
                .ToListAsync();
        }

        private IQueryable<BatchTake> BuildQuery(ServerDbContext context)
        {
            var query = context.BatchTakes
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var keyword = filter.Keyword.Trim();
                var hasNumeric = int.TryParse(keyword, out var numericValue);

                query = query.Where(x =>
                    x.TakenByUser.UserDisplayName.Contains(keyword) ||
                    x.Batch.OwnerUser.UserDisplayName.Contains(keyword) ||
                    x.Batch.Item.ItemName.Contains(keyword) ||
                    (hasNumeric && (x.BatchTakeId == numericValue || x.BatchId == numericValue)));
            }

            if (!string.IsNullOrWhiteSpace(filter.TakenByUserId))
            {
                query = query.Where(x => x.TakenByUserId == filter.TakenByUserId);
            }

            if (!string.IsNullOrWhiteSpace(filter.BatchOwnerUserId))
            {
                query = query.Where(x => x.Batch.OwnerUserId == filter.BatchOwnerUserId);
            }

            query = filter.BatchStatus switch
            {
                BatchStatusFilter.Active => query.Where(x => x.Batch.RemainingCount > 0),
                BatchStatusFilter.Ended => query.Where(x => x.Batch.RemainingCount <= 0),
                _ => query
            };

            if (filter.FromDate.HasValue)
            {
                var utcFrom = ToUtcStart(filter.FromDate.Value);
                query = query.Where(x => x.CreatedAtUtc >= utcFrom);
            }

            if (filter.ToDate.HasValue)
            {
                var utcToExclusive = ToUtcEndExclusive(filter.ToDate.Value);
                query = query.Where(x => x.CreatedAtUtc < utcToExclusive);
            }

            return query;
        }

        private static IQueryable<BatchTake> ApplySorting(
            IQueryable<BatchTake> query,
            TransactionSortBy sortBy,
            SortDirection sortDirection)
        {
            var desc = sortDirection == SortDirection.Desc;

            return (sortBy, desc) switch
            {
                (TransactionSortBy.TransactionId, false) => query.OrderBy(x => x.BatchTakeId),
                (TransactionSortBy.TransactionId, true) => query.OrderByDescending(x => x.BatchTakeId),

                (TransactionSortBy.User, false) => query.OrderBy(x => x.TakenByUser.UserDisplayName).ThenBy(x => x.BatchTakeId),
                (TransactionSortBy.User, true) => query.OrderByDescending(x => x.TakenByUser.UserDisplayName).ThenByDescending(x => x.BatchTakeId),

                (TransactionSortBy.BatchId, false) => query.OrderBy(x => x.BatchId).ThenBy(x => x.BatchTakeId),
                (TransactionSortBy.BatchId, true) => query.OrderByDescending(x => x.BatchId).ThenByDescending(x => x.BatchTakeId),

                (TransactionSortBy.ItemName, false) => query.OrderBy(x => x.Batch.Item.ItemName).ThenBy(x => x.BatchTakeId),
                (TransactionSortBy.ItemName, true) => query.OrderByDescending(x => x.Batch.Item.ItemName).ThenByDescending(x => x.BatchTakeId),

                (TransactionSortBy.Quantity, false) => query.OrderBy(x => x.Quantity).ThenBy(x => x.BatchTakeId),
                (TransactionSortBy.Quantity, true) => query.OrderByDescending(x => x.Quantity).ThenByDescending(x => x.BatchTakeId),

                (TransactionSortBy.Amount, false) => query
                    .OrderBy(x => x.Batch.BatchCount <= 0 ? 0m : ((decimal)x.Quantity * (decimal)x.Batch.Item.ItemPrice / x.Batch.BatchCount))
                    .ThenBy(x => x.BatchTakeId),

                (TransactionSortBy.Amount, true) => query
                    .OrderByDescending(x => x.Batch.BatchCount <= 0 ? 0m : ((decimal)x.Quantity * (decimal)x.Batch.Item.ItemPrice / x.Batch.BatchCount))
                    .ThenByDescending(x => x.BatchTakeId),

                (TransactionSortBy.CreatedAt, false) => query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.BatchTakeId),
                _ => query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.BatchTakeId),
            };
        }

        private async Task LoadAsync()
        {
            isLoading = true;
            StateHasChanged();

            expandedTransactionIds.Clear();
            batchUserSummaryLookup.Clear();
            batchHistoryLookup.Clear();

            using var context = await factory.CreateDbContextAsync();
            var query = BuildQuery(context);

            totalCount = await query.CountAsync();
            totalQuantity = await query.SumAsync(x => (int?)x.Quantity) ?? 0;
            uniqueUserCount = await query.Select(x => x.TakenByUserId).Distinct().CountAsync();
            uniqueBatchCount = await query.Select(x => x.BatchId).Distinct().CountAsync();
            filteredTotalEstimatedAmount = await query.SumAsync(x =>
                (decimal?)(x.Batch.BatchCount <= 0
                    ? 0m
                    : ((decimal)x.Quantity * (decimal)x.Batch.Item.ItemPrice / x.Batch.BatchCount))) ?? 0m;

            if (filter.Page > TotalPages)
            {
                filter.Page = TotalPages;
            }

            pagedTransactions = await ApplySorting(query, filter.SortBy, filter.SortDirection)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new TransactionListRow
                {
                    BatchTakeId = x.BatchTakeId,
                    BatchId = x.BatchId,
                    TakenByUserId = x.TakenByUserId,
                    TakenByDisplayName = x.TakenByUser.UserDisplayName,
                    BatchOwnerUserId = x.Batch.OwnerUserId,
                    BatchOwnerDisplayName = x.Batch.OwnerUser.UserDisplayName,
                    ItemName = x.Batch.Item.ItemName,
                    ItemDescription = x.Batch.Item.ItemDescription,
                    ItemPrice = (decimal)x.Batch.Item.ItemPrice,
                    ItemSize = (decimal)x.Batch.Item.ItemSize,
                    BatchCount = x.Batch.BatchCount,
                    RemainingCount = x.Batch.RemainingCount,
                    Quantity = x.Quantity,
                    RoastedAtUtc = x.Batch.RoastedAtUtc,
                    CreatedAtUtc = x.CreatedAtUtc,
                    EstimatedWeightGrams = x.Batch.BatchCount <= 0
                        ? 0m
                        : ((decimal)x.Quantity * (decimal)x.Batch.Item.ItemSize / x.Batch.BatchCount),
                    EstimatedPayableAmount = x.Batch.BatchCount <= 0
                        ? 0m
                        : ((decimal)x.Quantity * (decimal)x.Batch.Item.ItemPrice / x.Batch.BatchCount)
                })
                .ToListAsync();

            var batchIds = pagedTransactions
                .Select(x => x.BatchId)
                .Distinct()
                .ToArray();

            if (batchIds.Length > 0)
            {
                await LoadBatchAggregatesAsync(context, batchIds);
            }

            isLoading = false;
        }

        private async Task LoadBatchAggregatesAsync(ServerDbContext context, int[] batchIds)
        {
            var batchUserRaw = await (
                from take in context.BatchTakes.AsNoTracking()
                join batch in context.Batches.AsNoTracking() on take.BatchId equals batch.BatchId
                join item in context.Items.AsNoTracking() on batch.ItemId equals item.ItemId
                where batchIds.Contains(take.BatchId)
                group new { take, batch, item } by new
                {
                    take.BatchId,
                    take.TakenByUserId,
                    UserDisplayName = take.TakenByUser.UserDisplayName,
                    batch.BatchCount,
                    ItemPrice = (decimal)item.ItemPrice,
                    ItemSize = (decimal)item.ItemSize
                }
                into g
                select new
                {
                    g.Key.BatchId,
                    g.Key.TakenByUserId,
                    g.Key.UserDisplayName,
                    g.Key.BatchCount,
                    g.Key.ItemPrice,
                    g.Key.ItemSize,
                    Quantity = g.Sum(x => x.take.Quantity)
                })
                .ToListAsync();

            batchUserSummaryLookup = batchUserRaw
                .Select(x => new BatchUserSummaryRow
                {
                    BatchId = x.BatchId,
                    TakenByUserId = x.TakenByUserId,
                    UserDisplayName = x.UserDisplayName,
                    Quantity = x.Quantity,
                    WeightGrams = x.BatchCount <= 0 ? 0m : x.Quantity * (x.ItemSize / x.BatchCount),
                    PayableAmount = x.BatchCount <= 0 ? 0m : x.Quantity * (x.ItemPrice / x.BatchCount)
                })
                .GroupBy(x => x.BatchId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Quantity)
                          .ThenBy(x => x.UserDisplayName)
                          .ToList());

            var batchHistories = await context.BatchTakes
                .AsNoTracking()
                .Where(x => batchIds.Contains(x.BatchId))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new BatchHistoryRow
                {
                    BatchId = x.BatchId,
                    BatchTakeId = x.BatchTakeId,
                    TakenByDisplayName = x.TakenByUser.UserDisplayName,
                    Quantity = x.Quantity,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToListAsync();

            batchHistoryLookup = batchHistories
                .GroupBy(x => x.BatchId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Take(8).ToList());
        }

        private async Task ApplyFilterAsync()
        {
            filter.Page = 1;
            await LoadAsync();
        }

        private async Task ResetFilterAsync()
        {
            filter.Reset();
            await LoadAsync();
        }

        private async Task MovePageAsync(int page)
        {
            if (page < 1 || page > TotalPages || page == filter.Page)
            {
                return;
            }

            filter.Page = page;
            await LoadAsync();
        }

        private void ToggleExpand(int transactionId)
        {
            if (!expandedTransactionIds.Add(transactionId))
            {
                expandedTransactionIds.Remove(transactionId);
            }
        }

        private bool IsExpanded(int transactionId) => expandedTransactionIds.Contains(transactionId);

        private IReadOnlyList<BatchUserSummaryRow> GetBatchUserSummaryRows(int batchId)
            => batchUserSummaryLookup.TryGetValue(batchId, out var rows) ? rows : [];

        private IReadOnlyList<BatchHistoryRow> GetBatchHistoryRows(int batchId)
            => batchHistoryLookup.TryGetValue(batchId, out var rows) ? rows : [];

        private IEnumerable<int> GetVisiblePages()
        {
            var start = Math.Max(1, filter.Page - 2);
            var end = Math.Min(TotalPages, start + 4);

            if (end - start < 4)
            {
                start = Math.Max(1, end - 4);
            }

            return Enumerable.Range(start, end - start + 1);
        }

        private static DateTimeOffset ToUtcStart(DateTime localDate)
            => new DateTimeOffset(localDate.Date, KoreaOffset).ToUniversalTime();

        private static DateTimeOffset ToUtcEndExclusive(DateTime localDate)
            => new DateTimeOffset(localDate.Date.AddDays(1), KoreaOffset).ToUniversalTime();

        private static string FormatKrw(decimal amount)
            => $"{decimal.Round(amount, 0, MidpointRounding.AwayFromZero):N0}KRW";

        private static string FormatUnitKrw(decimal amount)
            => amount % 1 == 0
                ? $"{amount:N0}KRW"
                : $"{amount:N2}KRW";

        private static string FormatDateTime(DateTimeOffset utc)
            => utc.ToOffset(KoreaOffset).ToString("yyyy-MM-dd HH:mm");

        private static string GetDescriptionPreview(string? description)
            => string.IsNullOrWhiteSpace(description)
                ? "설명이 없습니다."
                : description.Length <= 52
                    ? description
                    : $"{description[..52]}…";

        private static string GetBatchStatusText(TransactionListRow tx)
            => tx.IsBatchActive ? "활성" : "종료";

        private static string GetBatchStatusBadgeClass(TransactionListRow tx)
            => tx.IsBatchActive ? "text-bg-success" : "text-bg-secondary";

        private static int GetRemainingPercent(TransactionListRow tx)
        {
            if (tx.BatchCount <= 0)
            {
                return 0;
            }

            return Math.Clamp((int)Math.Round(tx.RemainingCount * 100d / tx.BatchCount), 0, 100);
        }

        private static decimal GetUnitPrice(TransactionListRow tx)
            => tx.BatchCount <= 0 ? 0m : tx.ItemPrice / tx.BatchCount;

        private static string GetUnitWeightText(TransactionListRow tx)
            => tx.BatchCount <= 0 ? "-" : $"{(tx.ItemSize / tx.BatchCount):N0}g / 개";

        private static string GetRemainingWeightText(TransactionListRow tx)
            => tx.BatchCount <= 0 ? "-" : $"{(tx.RemainingCount * (tx.ItemSize / tx.BatchCount)):N0}g";

        private sealed class TransactionQueryModel
        {
            public string? Keyword { get; set; }
            public string? TakenByUserId { get; set; }
            public string? BatchOwnerUserId { get; set; }
            public BatchStatusFilter BatchStatus { get; set; } = BatchStatusFilter.All;
            public DateTime? FromDate { get; set; }
            public DateTime? ToDate { get; set; }
            public TransactionSortBy SortBy { get; set; } = TransactionSortBy.CreatedAt;
            public SortDirection SortDirection { get; set; } = SortDirection.Desc;
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 20;

            public void Reset()
            {
                Keyword = null;
                TakenByUserId = null;
                BatchOwnerUserId = null;
                BatchStatus = BatchStatusFilter.All;
                FromDate = null;
                ToDate = null;
                SortBy = TransactionSortBy.CreatedAt;
                SortDirection = SortDirection.Desc;
                Page = 1;
                PageSize = 20;
            }
        }

        private sealed class UserOption
        {
            public string UserId { get; set; } = string.Empty;
            public string UserDisplayName { get; set; } = string.Empty;
        }

        private sealed class TransactionListRow
        {
            public int BatchTakeId { get; set; }
            public int BatchId { get; set; }
            public string TakenByUserId { get; set; } = string.Empty;
            public string TakenByDisplayName { get; set; } = string.Empty;
            public string BatchOwnerUserId { get; set; } = string.Empty;
            public string BatchOwnerDisplayName { get; set; } = string.Empty;
            public string ItemName { get; set; } = string.Empty;
            public string? ItemDescription { get; set; }
            public decimal ItemPrice { get; set; }
            public decimal ItemSize { get; set; }
            public int BatchCount { get; set; }
            public int RemainingCount { get; set; }
            public int Quantity { get; set; }
            public DateTimeOffset RoastedAtUtc { get; set; }
            public DateTimeOffset CreatedAtUtc { get; set; }
            public decimal EstimatedWeightGrams { get; set; }
            public decimal EstimatedPayableAmount { get; set; }
            public bool IsBatchActive => RemainingCount > 0;
        }

        private sealed class BatchUserSummaryRow
        {
            public int BatchId { get; set; }
            public string TakenByUserId { get; set; } = string.Empty;
            public string UserDisplayName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal WeightGrams { get; set; }
            public decimal PayableAmount { get; set; }
        }

        private sealed class BatchHistoryRow
        {
            public int BatchId { get; set; }
            public int BatchTakeId { get; set; }
            public string TakenByDisplayName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public DateTimeOffset CreatedAtUtc { get; set; }
        }

        private enum BatchStatusFilter
        {
            All,
            Active,
            Ended
        }

        private enum TransactionSortBy
        {
            CreatedAt,
            TransactionId,
            User,
            BatchId,
            ItemName,
            Quantity,
            Amount
        }

        private enum SortDirection
        {
            Asc,
            Desc
        }
    }
}