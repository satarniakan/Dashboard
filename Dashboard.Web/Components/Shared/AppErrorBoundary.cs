using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Dashboard.Web.Components.Shared;

// این کلاس همان ErrorBoundary داخلی بلیزور است، فقط OnErrorAsync آن override شده
// تا هر خطای مدیریت‌نشده‌ی رندر، هم لاگ شود (برای خودمان) و هم یک toast خطا
// به کاربر نشان دهد (چون UI خود ErrorBoundary فقط داخل همان بخش از صفحه دیده می‌شود).
public class AppErrorBoundary : ErrorBoundary
{
    [Inject]
    private ILogger<AppErrorBoundary> Logger { get; set; } = default!;

    [Inject]
    private Dashboard.Web.Services.ToastService ToastService { get; set; } = default!;

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "خطای مدیریت‌نشده هنگام رندر یک کامپوننت");
        ToastService.ShowError(ErrorMessageHelper.ToUserMessage(exception));
        return base.OnErrorAsync(exception);
    }
}