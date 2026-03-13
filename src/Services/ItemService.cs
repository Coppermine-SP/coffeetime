using coffeetime.Contexts;
using coffeetime.Models;
using Microsoft.EntityFrameworkCore;

namespace coffeetime.Services
{
    public class ItemService(IDbContextFactory<ServerDbContext> factory, ILogger<ItemService> logger)
    {
        public async Task AddItemsAsync(Item item)
        {
            using var context = await factory.CreateDbContextAsync();
            await context.Items.AddAsync(item);
            await context.SaveChangesAsync();
            logger.LogInformation($"Added item {item.ItemName} to the database.", item.ItemId);
        }

        public async Task EditItemAsync(Item item)
        {

        }

        public async Task DeleteItemAsync(int itemId)
        {

        }
    }
}
