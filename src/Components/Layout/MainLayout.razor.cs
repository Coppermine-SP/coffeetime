using coffeetime.Components.Modal;
using coffeetime.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace coffeetime.Components.Layout
{
    public partial class MainLayout(ILogger<MainLayout> logger, ModalService modal, NavigationManager navi) : LayoutComponentBase
    {
        [CascadingParameter]
        private Task<AuthenticationState>? authenticationStateTask { get; set; }

        private ClaimsPrincipal? principal;
        protected override async Task OnInitializedAsync()
        {
            if (authenticationStateTask != null)
            {
                var authState = await authenticationStateTask;
                principal = authState.User!;
            }
        }

        private async Task OnInfoBtnClickAsync()
        {
            await modal.ShowAsync<Modal.AboutModal, string>("정보");
        }

        private async Task OnSsoSignoutBtnClickAsync()
        {
            string innerHtml = "클라우드인터렉티브 통합 인증에서 로그아웃 하시겠습니까?<br><strong>모든 클라우드인터렉티브 앱에서 로그아웃합니다.</strong>";
            var result = await modal.ShowAsync<AlertModal, bool>("로그아웃", ModalService.Params()
                .Add("InnerHtml", innerHtml)
                .Add("IsCancelable", true)
                .Build());

            if (result is { IsCancelled: false, Value: true })
            {
                navi.NavigateTo("/oauth/signout?singleSignout=true", forceLoad: true);
            }
        }

        private async Task OnSignoutBtnClickAsync()
        {
            string innerHtml = "로그아웃 하시겠습니까?";
            var result = await modal.ShowAsync<AlertModal, bool>("로그아웃", ModalService.Params()
                .Add("InnerHtml", innerHtml)
                .Add("IsCancelable", true)
                .Build());

            if (result is { IsCancelled: false, Value: true })
            {
                navi.NavigateTo("/oauth/signout?singleSignout=false", forceLoad: true);
            }
        }
    }
}
