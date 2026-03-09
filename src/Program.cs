using coffeetime.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

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

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");

                // API 호출이나 토큰 재사용이 필요할 때만 유지
                options.SaveTokens = true;

                // ADFS가 내보내는 claim 이름을 그대로 쓰고 싶을 때
                options.MapInboundClaims = false;

                // ADFS가 실제로 어떤 claim을 내보내는지 보고 맞춰야 함
                // options.TokenValidationParameters = new TokenValidationParameters
                // {
                //     NameClaimType = "upn",
                //     RoleClaimType = "role"
                // };
            });
            var app = builder.Build();
            app.MapGet("/account/login", (string? returnUrl) =>
            {
                return Results.Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl
                    },
                    new[] { OpenIdConnectDefaults.AuthenticationScheme });
            });

            app.MapGet("/account/logout", () =>
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
            });


            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

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
