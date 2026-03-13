using coffeetime.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace coffeetime.Services
{
    public class UserService(
        IDbContextFactory<ServerDbContext> factory,
        ILogger<UserService> logger,
        IMemoryCache cache)
    {
        public async Task CreateOrUpdateUserCacheAsync(string userObjectGuid, string userDisplayName)
        {
            using var context = await factory.CreateDbContextAsync();

            logger.LogInformation($"Update UserCache {userObjectGuid} => {userDisplayName}");
            int rows = await context.UserCaches.
                Where(u => u.UserObjectGuid == userObjectGuid).
                ExecuteUpdateAsync(u => u.SetProperty(uc => uc.UserDisplayName, userDisplayName));

            if (rows == 0)
            {
                logger.LogInformation($"Create UserCache {userObjectGuid} => {userDisplayName}");
                await context.UserCaches.AddAsync(new Models.UserCache
                {
                    UserObjectGuid = userObjectGuid,
                    UserDisplayName = userDisplayName
                });

                await context.SaveChangesAsync();
            }

            cache.Set(userObjectGuid, userDisplayName, TimeSpan.FromHours(8));
        }

        public async Task<string> GetUserDisplayName(string userObjectGuid)
        {
            if (cache.TryGetValue(userObjectGuid, out string? userDisplayName))
            {
                logger.LogInformation($"Found UserCache from in-memory cache {userObjectGuid} => {userDisplayName}");
                return userDisplayName!;
            }
            using var context = await factory.CreateDbContextAsync();

            return (await context.UserCaches.
                AsNoTracking().
                FirstAsync(
                x => x.UserObjectGuid == userObjectGuid)).UserDisplayName;
        }
    }
}
