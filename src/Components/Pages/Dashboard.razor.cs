using coffeetime.Components.Modal;
using coffeetime.Contexts;
using coffeetime.Models;
using coffeetime.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace coffeetime.Components.Pages
{
    public partial class Dashboard(IDbContextFactory<ServerDbContext> factory, ModalService modal, ItemService item, BatchService batchService) : ComponentBase
    {
        [CascadingParameter]
        private Task<AuthenticationState>? authenticationStateTask { get; set; }

        private ClaimsPrincipal? principal;
        public ICollection<PackageBatch> activeBatches = [];
        public ICollection<BatchTake> recentTakes = [];
        public ICollection<Item> items = [];

        protected override async Task OnInitializedAsync()
        {
            if (authenticationStateTask != null)
            {
                var authState = await authenticationStateTask;
                principal = authState.User!;
            }

            using var context = await factory.CreateDbContextAsync();

            activeBatches = await context.Batches
                .Where(x => x.RemainingCount > 0)
                .OrderBy(x => x.RoastedAtUtc)
                .ThenBy(x => x.RemainingCount)
                .Include(x => x.Item)
                .Include(x => x.OwnerUser)
                .AsNoTracking()
                .Take(10)
                .ToListAsync();

            recentTakes = await context.BatchTakes
                .Include(x => x.TakenByUser)
                .OrderByDescending(x => x.CreatedAtUtc)
                .AsNoTracking()
                .Take(20)
                .ToListAsync();

            //This might cause a performance problem if the item collection is very huge.
            items = await context.Items
                .AsNoTracking()
                .OrderBy(x => x.ItemName)
                .ToListAsync();
        }

        private async Task OnItemAddBtnClick()
        {
            var result = await modal.ShowAsync<EditItemModal, Item>("원두 추가");
            if (!result.IsCancelled && result.Value is not null)
            {
                await item.AddItemAsync(result.Value!);
            }

            await OnInitializedAsync();
        }

        private async Task OnItemEditBtnClick(Item x)
        {
            var result = await modal.ShowAsync<EditItemModal, Item>("원두 편집",
                new ModalParameterBuilder()
                .Add("Item", x)
                .Build());

            if (!result.IsCancelled && result.Value is not null)
            {
                await item.UpdateItemAsync(result.Value!);
            }

            StateHasChanged();
        }

        private async Task OnBatchAddBtnClick()
        {
            var items = await factory.CreateDbContextAsync()
                .Result.Items
                .AsNoTracking()
                .ToListAsync();

            var result = await modal.ShowAsync<AddBatchModal, PackageBatch>("새 배치 추가", new ModalParameterBuilder()
                .Add("Items", items)
                .Build());

            if (!result.IsCancelled && result.Value is not null)
            {
                var value = result.Value!;
                value.RemainingCount = value.BatchCount;
                value.OwnerUserId = principal!.FindFirstValue("oid")!;
                await batchService.AddBatchAsync(value);
            }

            await OnInitializedAsync();
        }

        private async Task OnItemDeleteBtnClick(Item x)
        {
            var result = await modal.ShowAsync<ConfirmDeleteModal, bool>("경고");

            if (!result.IsCancelled && result.Value)
            {
                await item.DeleteItemAsync(x.ItemId);
            }

            await OnInitializedAsync();
        }

        private async Task OnBatchTakeBtnClick(PackageBatch batch)
        {
            var result = await modal.ShowAsync<AddBatchTake, int?>("새 가져가기", new ModalParameterBuilder()
                .Add("Batch", batch)
                .Build());

            if (!result.IsCancelled && result.Value is not null)
            {
                var x = new BatchTake
                {
                    BatchId = batch.BatchId,
                    TakenByUserId = principal!.FindFirstValue("oid")!,
                    Quantity = result.Value.Value,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await batchService.AddBatchTakeAsync(x);
            }

            await OnInitializedAsync();
        }

        private static string GetBatchTotalText(PackageBatch batch)
            => $"{batch.BatchCount:N0}개 / 각 {(batch.Item.ItemSize / batch.BatchCount):N0}g";

        private static string GetBatchRemainingText(PackageBatch batch)
            => $"{batch.RemainingCount:N0}개 / {(batch.Item.ItemSize - ((batch.Item.ItemSize / batch.BatchCount) * (batch.BatchCount - batch.RemainingCount))):N0}g";

        private static string FormatTakeTime(DateTimeOffset utc)
            => utc.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm:ss");
        private static int GetRemainingPercent(PackageBatch batch)
        {
            if (batch.BatchCount <= 0)
                return 0;

            var percent = (int)Math.Round(batch.RemainingCount * 100d / batch.BatchCount);
            return Math.Clamp(percent, 0, 100);
        }
    }
}
