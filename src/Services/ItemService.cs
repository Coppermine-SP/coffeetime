using coffeetime.Contexts;
using coffeetime.Models;
using Microsoft.EntityFrameworkCore;

namespace coffeetime.Services
{
    public class ItemService(IDbContextFactory<ServerDbContext> factory, ILogger<ItemService> logger)
    {
        public async Task AddItemAsync(Item item)
        {
            using var context = await factory.CreateDbContextAsync();
            await context.Items.AddAsync(item);
            await context.SaveChangesAsync();
            logger.LogInformation($"Added item {item.ItemName} to the database.");
        }

        public async Task UpdateItemAsync(Item item)
        {
            using var context = await factory.CreateDbContextAsync();
            try
            {
                context.Items.Update(item);
                await context.SaveChangesAsync();
            }
            catch(Exception e)
            {
                logger.LogError("Exception occured while update database: " + e.ToString());
            }
            logger.LogInformation($"Updated item #{item.ItemId} to the database.");
        }

        public async Task DeleteItemAsync(int itemId)
        {
            using var context = await factory.CreateDbContextAsync();
            await context.Items
                .Where(x => x.ItemId == itemId)
                .ExecuteDeleteAsync();

            logger.LogInformation($"Deleted item #{itemId} to the database.");
        }
    }
}
