using coffeetime.Components.Modal;
using coffeetime.Contexts;
using coffeetime.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace coffeetime.Components.Pages
{
    public partial class Dashboard(IDbContextFactory<ServerDbContext> factory) : ComponentBase
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
                .ToListAsync();
        }
    }
}
