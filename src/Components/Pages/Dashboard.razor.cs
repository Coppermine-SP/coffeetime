using coffeetime.Components.Modal;
using coffeetime.Contexts;
using coffeetime.Models;
using coffeetime.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace coffeetime.Components.Pages
{
    public partial class Dashboard(IDbContextFactory<ServerDbContext> factory, ModalService modal, ItemService item, BatchService batch) : ComponentBase
    {
        public ICollection<PackageBatch> activeBatches = [];
        public ICollection<BatchTake> recentTakes = [];
        public ICollection<Item> items = [];

        protected override async Task OnInitializedAsync()
        {
            using var context = await factory.CreateDbContextAsync();

            activeBatches = await context.Batches
                .Where(x => x.RemainingCount > 0)
                .OrderBy(x => x.RoastedAtUtc)
                .ThenBy(x => x.RemainingCount)
                .AsNoTracking()
                .Take(10)
                .ToListAsync();

            recentTakes = await context.BatchTakes
                .OrderBy(x => x.CreatedAtUtc)
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

        private async Task OnItemDeleteBtnClick(Item x)
        {
            var result = await modal.ShowAsync<ConfirmDeleteModal, bool>("경고");

            if (!result.IsCancelled && result.Value)
            {
                await item.DeleteItemAsync(x.ItemId);
            }

            await OnInitializedAsync();
        }
    }
}
