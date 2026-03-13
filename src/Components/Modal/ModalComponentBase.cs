using Microsoft.AspNetCore.Components;

namespace coffeetime.Components.Modal
{
    public class ModalComponentBase : ComponentBase
    {
        [Parameter] public Func<object?, Task> Close { get; set; } = _ => Task.CompletedTask;

        protected Task CloseModal(object? result = null) => Close(result);
    }
}
