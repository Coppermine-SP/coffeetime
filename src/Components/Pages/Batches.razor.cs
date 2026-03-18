using coffeetime.Contexts;
using coffeetime.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace coffeetime.Components.Pages
{
    public partial class Batches(IDbContextFactory<ServerDbContext> factory) : ComponentBase
    {
        private readonly BatchQueryModel filter = new();
        private readonly HashSet<int> expandedBatchIds = [];

        private List<BatchListRow> pagedBatches = [];
        private Dictionary<int, List<UserSettlementRow>> settlementLookup = [];
        private Dictionary<int, List<TakeHistoryRow>> takeHistoryLookup = [];
        private List<OwnerOption> ownerOptions = [];

        private bool isLoading;
        private int totalCount;
        private int activeCount;
        private int endedCount;
        private decimal filteredTotalBatchPrice;

        private int TotalPages => Math.Max(1, (int)Math.Ceiling(totalCount / (double)filter.PageSize));
        private int StartRow => totalCount == 0 ? 0 : ((filter.Page - 1) * filter.PageSize) + 1;
        private int EndRow => totalCount == 0 ? 0 : Math.Min(filter.Page * filter.PageSize, totalCount);
        private decimal CurrentPageSettledAmount => pagedBatches.Sum(x => x.SettledAmount);

        protected override async Task OnInitializedAsync()
        {
            await LoadOwnerOptionsAsync();
            await LoadAsync();
        }

        private async Task LoadOwnerOptionsAsync()
        {
            using var context = await factory.CreateDbContextAsync();

            ownerOptions = await context.Batches
                .AsNoTracking()
                .GroupBy(x => new { x.OwnerUserId, x.OwnerUser.UserDisplayName })
                .Select(g => new OwnerOption
                {
                    UserId = g.Key.OwnerUserId,
                    UserDisplayName = g.Key.UserDisplayName
                })
                .OrderBy(x => x.UserDisplayName)
                .ToListAsync();
        }

        private IQueryable<PackageBatch> BuildQuery(ServerDbContext context)
        {
            var query = context.Batches
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var keyword = filter.Keyword.Trim();
                var hasBatchId = int.TryParse(keyword, out var batchId);

                query = query.Where(x =>
                    x.Item.ItemName.Contains(keyword) ||
                    x.OwnerUser.UserDisplayName.Contains(keyword) ||
                    (hasBatchId && x.BatchId == batchId));
            }

            if (!string.IsNullOrWhiteSpace(filter.OwnerUserId))
            {
                query = query.Where(x => x.OwnerUserId == filter.OwnerUserId);
            }

            query = filter.Status switch
            {
                BatchStatusFilter.Active => query.Where(x => x.RemainingCount > 0),
                BatchStatusFilter.Ended => query.Where(x => x.RemainingCount <= 0),
                _ => query
            };

            return query;
        }

        private static IQueryable<PackageBatch> ApplySorting(
            IQueryable<PackageBatch> query,
            BatchSortBy sortBy,
            SortDirection sortDirection)
        {
            var desc = sortDirection == SortDirection.Desc;

            return (sortBy, desc) switch
            {
                (BatchSortBy.BatchId, false) => query.OrderBy(x => x.BatchId),
                (BatchSortBy.BatchId, true) => query.OrderByDescending(x => x.BatchId),

                (BatchSortBy.ItemName, false) => query.OrderBy(x => x.Item.ItemName).ThenBy(x => x.BatchId),
                (BatchSortBy.ItemName, true) => query.OrderByDescending(x => x.Item.ItemName).ThenByDescending(x => x.BatchId),

                (BatchSortBy.Owner, false) => query.OrderBy(x => x.OwnerUser.UserDisplayName).ThenBy(x => x.BatchId),
                (BatchSortBy.Owner, true) => query.OrderByDescending(x => x.OwnerUser.UserDisplayName).ThenByDescending(x => x.BatchId),

                (BatchSortBy.BatchCount, false) => query.OrderBy(x => x.BatchCount).ThenBy(x => x.BatchId),
                (BatchSortBy.BatchCount, true) => query.OrderByDescending(x => x.BatchCount).ThenByDescending(x => x.BatchId),

                (BatchSortBy.RemainingCount, false) => query.OrderBy(x => x.RemainingCount).ThenBy(x => x.BatchId),
                (BatchSortBy.RemainingCount, true) => query.OrderByDescending(x => x.RemainingCount).ThenByDescending(x => x.BatchId),

                (BatchSortBy.Price, false) => query.OrderBy(x => x.Item.ItemPrice).ThenBy(x => x.BatchId),
                (BatchSortBy.Price, true) => query.OrderByDescending(x => x.Item.ItemPrice).ThenByDescending(x => x.BatchId),

                (BatchSortBy.RoastedAt, false) => query.OrderBy(x => x.RoastedAtUtc).ThenBy(x => x.BatchId),
                _ => query.OrderByDescending(x => x.RoastedAtUtc).ThenByDescending(x => x.BatchId),
            };
        }

        private async Task LoadAsync()
        {
            isLoading = true;
            StateHasChanged();

            expandedBatchIds.Clear();
            settlementLookup.Clear();
            takeHistoryLookup.Clear();

            using var context = await factory.CreateDbContextAsync();
            var query = BuildQuery(context);

            var summary = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Active = g.Count(x => x.RemainingCount > 0),
                    Ended = g.Count(x => x.RemainingCount <= 0),
                    TotalBatchPrice = g.Sum(x => (decimal?)x.Item.ItemPrice) ?? 0m
                })
                .FirstOrDefaultAsync();

            totalCount = summary?.Total ?? 0;
            activeCount = summary?.Active ?? 0;
            endedCount = summary?.Ended ?? 0;
            filteredTotalBatchPrice = summary?.TotalBatchPrice ?? 0m;

            if (filter.Page > TotalPages)
            {
                filter.Page = TotalPages;
            }

            pagedBatches = await ApplySorting(query, filter.SortBy, filter.SortDirection)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new BatchListRow
                {
                    BatchId = x.BatchId,
                    ItemId = x.ItemId,
                    ItemName = x.Item.ItemName,
                    ItemDescription = x.Item.ItemDescription,
                    ItemPrice = (decimal)x.Item.ItemPrice,
                    ItemSize = (decimal)x.Item.ItemSize,
                    OwnerUserId = x.OwnerUserId,
                    OwnerDisplayName = x.OwnerUser.UserDisplayName,
                    BatchCount = x.BatchCount,
                    RemainingCount = x.RemainingCount,
                    RoastedAtUtc = x.RoastedAtUtc
                })
                .ToListAsync();

            var batchIds = pagedBatches.Select(x => x.BatchId).ToArray();
            if (batchIds.Length > 0)
            {
                await LoadAggregateDataAsync(context, batchIds);
            }

            isLoading = false;
        }

        private async Task LoadAggregateDataAsync(ServerDbContext context, int[] batchIds)
        {
            var batchStats = await context.BatchTakes
                .AsNoTracking()
                .Where(x => batchIds.Contains(x.BatchId))
                .GroupBy(x => x.BatchId)
                .Select(g => new
                {
                    BatchId = g.Key,
                    TotalTakenCount = g.Sum(x => x.Quantity),
                    ParticipantCount = g.Select(x => x.TakenByUserId).Distinct().Count()
                })
                .ToListAsync();

            var statMap = batchStats.ToDictionary(x => x.BatchId);

            foreach (var batch in pagedBatches)
            {
                if (statMap.TryGetValue(batch.BatchId, out var stat))
                {
                    batch.TotalTakenCount = stat.TotalTakenCount;
                    batch.ParticipantCount = stat.ParticipantCount;
                }
            }

            var settlementRaw = await (
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

            var settlements = settlementRaw
                .Select(x => new UserSettlementRow
                {
                    BatchId = x.BatchId,
                    TakenByUserId = x.TakenByUserId,
                    UserDisplayName = x.UserDisplayName,
                    Quantity = x.Quantity,
                    WeightGrams = x.BatchCount <= 0 ? 0m : x.Quantity * (x.ItemSize / x.BatchCount),
                    PayableAmount = x.BatchCount <= 0
                        ? 0m
                        : Math.Round(x.Quantity * (x.ItemPrice / x.BatchCount), 0, MidpointRounding.AwayFromZero)
                })
                .ToList();

            settlementLookup = settlements
                .GroupBy(x => x.BatchId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Quantity)
                          .ThenBy(x => x.UserDisplayName)
                          .ToList());

            foreach (var batch in pagedBatches)
            {
                if (settlementLookup.TryGetValue(batch.BatchId, out var rows))
                {
                    batch.SettledAmount = rows.Sum(x => x.PayableAmount);
                }
            }

            var histories = await context.BatchTakes
                .AsNoTracking()
                .Where(x => batchIds.Contains(x.BatchId))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new TakeHistoryRow
                {
                    BatchId = x.BatchId,
                    BatchTakeId = x.BatchTakeId,
                    UserDisplayName = x.TakenByUser.UserDisplayName,
                    Quantity = x.Quantity,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToListAsync();

            takeHistoryLookup = histories
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

        private void ToggleExpand(int batchId)
        {
            if (!expandedBatchIds.Add(batchId))
            {
                expandedBatchIds.Remove(batchId);
            }
        }

        private bool IsExpanded(int batchId) => expandedBatchIds.Contains(batchId);

        private IReadOnlyList<UserSettlementRow> GetSettlementRows(int batchId)
            => settlementLookup.TryGetValue(batchId, out var rows) ? rows : [];

        private IReadOnlyList<TakeHistoryRow> GetTakeHistoryRows(int batchId)
            => takeHistoryLookup.TryGetValue(batchId, out var rows) ? rows : [];

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

        private static string FormatKrw(decimal amount) => $"{amount:N0}KRW";

        private static string FormatUnitKrw(decimal amount)
            => amount % 1 == 0 ? $"{amount:N0}KRW" : $"{amount:N2}KRW";

        private static string FormatDateTime(DateTimeOffset utc)
            => utc.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm");

        private static string GetStatusText(BatchListRow batch)
            => batch.IsActive ? "활성" : "종료";

        private static string GetStatusBadgeClass(BatchListRow batch)
            => batch.IsActive ? "text-bg-success" : "text-bg-secondary";

        private static int GetRemainingPercent(BatchListRow batch)
        {
            if (batch.BatchCount <= 0)
            {
                return 0;
            }

            return Math.Clamp((int)Math.Round(batch.RemainingCount * 100d / batch.BatchCount), 0, 100);
        }

        private static int GetSettlementPercent(BatchListRow batch)
        {
            if (batch.ItemPrice <= 0)
            {
                return 0;
            }

            return Math.Clamp((int)Math.Round((double)(batch.SettledAmount / batch.ItemPrice * 100m)), 0, 100);
        }

        private static string GetEachWeightText(BatchListRow batch)
            => batch.BatchCount <= 0 ? "-" : $"{(batch.ItemSize / batch.BatchCount):N0}g / 개";

        private static string GetRemainingWeightText(BatchListRow batch)
            => batch.BatchCount <= 0 ? "-" : $"{(batch.RemainingCount * (batch.ItemSize / batch.BatchCount)):N0}g";

        private static string GetDescriptionPreview(string? description)
            => string.IsNullOrWhiteSpace(description)
                ? "설명이 없습니다."
                : description.Length <= 52
                    ? description
                    : $"{description[..52]}…";

        private sealed class BatchQueryModel
        {
            public string? Keyword { get; set; }
            public string? OwnerUserId { get; set; }
            public BatchStatusFilter Status { get; set; } = BatchStatusFilter.All;
            public BatchSortBy SortBy { get; set; } = BatchSortBy.RoastedAt;
            public SortDirection SortDirection { get; set; } = SortDirection.Desc;
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 10;

            public void Reset()
            {
                Keyword = null;
                OwnerUserId = null;
                Status = BatchStatusFilter.All;
                SortBy = BatchSortBy.RoastedAt;
                SortDirection = SortDirection.Desc;
                Page = 1;
                PageSize = 10;
            }
        }

        private sealed class OwnerOption
        {
            public string UserId { get; set; } = string.Empty;
            public string UserDisplayName { get; set; } = string.Empty;
        }

        private sealed class BatchListRow
        {
            public int BatchId { get; set; }
            public int ItemId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string? ItemDescription { get; set; }
            public decimal ItemPrice { get; set; }
            public decimal ItemSize { get; set; }
            public int BatchCount { get; set; }
            public int RemainingCount { get; set; }
            public string OwnerUserId { get; set; } = string.Empty;
            public string OwnerDisplayName { get; set; } = string.Empty;
            public DateTimeOffset RoastedAtUtc { get; set; }
            public int ParticipantCount { get; set; }
            public int TotalTakenCount { get; set; }
            public decimal SettledAmount { get; set; }
            public bool IsActive => RemainingCount > 0;
        }

        private sealed class UserSettlementRow
        {
            public int BatchId { get; set; }
            public string TakenByUserId { get; set; } = string.Empty;
            public string UserDisplayName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal WeightGrams { get; set; }
            public decimal PayableAmount { get; set; }
        }

        private sealed class TakeHistoryRow
        {
            public int BatchId { get; set; }
            public int BatchTakeId { get; set; }
            public string UserDisplayName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public DateTimeOffset CreatedAtUtc { get; set; }
        }

        private enum BatchStatusFilter
        {
            All,
            Active,
            Ended
        }

        private enum BatchSortBy
        {
            RoastedAt,
            BatchId,
            ItemName,
            Owner,
            BatchCount,
            RemainingCount,
            Price
        }

        private enum SortDirection
        {
            Asc,
            Desc
        }
    }
}