using coffeetime.Components;
using coffeetime.Contexts;
using coffeetime.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Net;

namespace coffeetime
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var builder = WebApplication.CreateBuilder(args);

            // Docker containerized environment configuration
            builder.Configuration.AddJsonFile(@"C:\config\appsettings.json", optional: true, reloadOnChange: true);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddDbContextFactory<ServerDbContext>(opt =>
                 opt.UseMySQL(builder.Configuration.GetSection("ConnectionStrings")["DefaultConnection"]!,
                 o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                var cfg = builder.Configuration.GetSection("Proxy");
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto |
                    ForwardedHeaders.XForwardedHost;

                options.KnownProxies.Clear();

                options.KnownProxies.Add(IPAddress.Parse(cfg["AllowedProxy"]!));
            });
            builder.Services.AddMemoryCache();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddAuthorization();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                var cfg = builder.Configuration.GetSection("Authentication:Adfs");

                options.Authority = cfg["Authority"];
                options.ClientId = cfg["ClientId"];
                options.ClientSecret = cfg["ClientSecret"];

                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.RequireHttpsMetadata = true;

                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";
                options.RemoteSignOutPath = "/signout-oidc";
                options.ResponseType = "code";
                options.ResponseMode = "form_post";
                options.Resource = cfg["ResourceId"];

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("allatclaims");

                options.SaveTokens = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name"
                };
                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userObjectGuid = context.Principal?.FindFirst("oid")?.Value;
                        var userDisplayName = context.Principal?.FindFirst("name")?.Value;
                        if (!string.IsNullOrWhiteSpace(userObjectGuid) && !string.IsNullOrWhiteSpace(userDisplayName))
                        {
                            var userService = context.HttpContext.RequestServices.GetRequiredService<UserService>();
                            await userService.CreateOrUpdateUserCacheAsync(userObjectGuid, userDisplayName);
                        }
                    }
                };

            });
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<ModalService>();
            builder.Services.AddScoped<BatchService>();
            builder.Services.AddScoped<ItemService>();
            builder.Services.AddScoped<UserService>();
            var app = builder.Build();
            app.UseForwardedHeaders();
            app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next(context);
            });
            app.MapGet("/oauth/signin", (string? returnUrl) =>
            {
                return Results.Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/dashboard" : returnUrl
                    },
                    new[] { OpenIdConnectDefaults.AuthenticationScheme });
            });

            app.MapGet("/oauth/signout", (bool singleSignOut) =>
            {
                if (singleSignOut)
                {
                    return Results.SignOut(
                        new AuthenticationProperties
                        {
                            RedirectUri = "/"
                        },
                        new[]
                        {
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        OpenIdConnectDefaults.AuthenticationScheme
                        });
                }
                else
                {
                    return Results.SignOut(
                        new AuthenticationProperties
                        {
                            RedirectUri = "/"
                        },
                        new[]
                        {
                        CookieAuthenticationDefaults.AuthenticationScheme
                        });
                }
            });

            app.UseAntiforgery();
            app.MapStaticAssets();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode(o => o.DisableWebSocketCompression = true);
            app.Run();
        }
    }
}
