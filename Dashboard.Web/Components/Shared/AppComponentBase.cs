// Dashboard.Web/Components/Shared/AppComponentBase.cs
using Microsoft.AspNetCore.Components;

namespace Dashboard.Web.Components.Shared;

/// <summary>
/// کلاس پایه‌ی اختیاری کامپوننت‌ها — الگوی مدیریت خطای رویدادهای async.
///
/// زمینه: ErrorBoundary خطاهای «رندر» را می‌گیرد، اما استثنای داخل رویدادهای async
/// (@onclick و ...) اگر مدیریت نشود مدار (Circuit) را می‌اندازد و کاربر با
/// «Reconnecting» و از دست رفتن وضعیت مواجه می‌شود. برای رویدادهای حساس، بدنه را
/// داخل RunSafe بپیچید:
///
/// <code>
/// protected override Task OnInitializedAsync() => RunSafe(async () =>
/// {
///     // ... منطق صفحه
/// });
///
/// private Task Save() => RunSafe(async () =>
/// {
///     await Service.SaveAsync(model);
///     Toast.ShowSuccess("ذخیره شد.");
/// });
/// </code>
///
/// خطا لاگ می‌شود، به کاربر toast فارسی نشان داده می‌شود و مدار زنده می‌ماند.
/// </summary>
public abstract class AppComponentBase : ComponentBase
{
    [Inject] protected ILogger<AppComponentBase> Logger { get; set; } = default!;
    [Inject] protected Services.ToastService Toast { get; set; } = default!;

    /// <summary>اجرا یا مدیریت‌شده‌ی یک عملیات async؛ خطا را لاگ می‌کند و toast فارسی نشان می‌دهد.</summary>
    protected async Task RunSafe(Func<Task> action, string? userMessage = null, [System.Runtime.CompilerServices.CallerMemberName] string context = "")
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // لغو عمدی (بستن صفحه/مدار) — بی‌صدا
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "خطا در {Context}", context);
            Toast.ShowError(userMessage ?? ErrorMessageHelper.ToUserMessage(ex));
        }
    }

    /// <summary>همان RunSafe با مقدار برگشتی؛ در صورت خطا default برمی‌گردد.</summary>
    protected async Task<T?> RunSafe<T>(Func<Task<T>> action, string? userMessage = null, [System.Runtime.CompilerServices.CallerMemberName] string context = "")
    {
        try
        {
            return await action();
        }
        catch (OperationCanceledException)
        {
            return default;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "خطا در {Context}", context);
            Toast.ShowError(userMessage ?? ErrorMessageHelper.ToUserMessage(ex));
            return default;
        }
    }
}
