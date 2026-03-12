using coffeetime.Components;
using coffeetime.Contexts;
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
            var builder = WebApplication.CreateBuilder(args);

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

                options.KnownProxies.Add(IPAddress.Parse(cfg["AllowedProxy"]!));
                options.AllowedHosts.Add(cfg["AllowedHost"]!);
            });
            
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

            });
            var app = builder.Build();
            app.MapGet("/oauth/signin", (string? returnUrl) =>
            {
                return Results.Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/main" : returnUrl
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

            app.UseForwardedHeaders();
            app.UseHttpsRedirection();
            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.MapStaticAssets();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
