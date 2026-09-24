namespace Dashboard.Web.Services;

public enum ToastType { Success, Error, Info }

// Id یکتا: چون record است و @key در Blazor با برابری مقداری کار می‌کند،
// بدون Id دو توست با متن یکسان کلید تکراری می‌سازند و رندر خطا می‌دهد
public record ToastMessage(Guid Id, string Text, ToastType Type);

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message) => OnShow?.Invoke(new ToastMessage(Guid.NewGuid(), message, ToastType.Success));
    public void ShowError(string message) => OnShow?.Invoke(new ToastMessage(Guid.NewGuid(), message, ToastType.Error));
    public void ShowInfo(string message) => OnShow?.Invoke(new ToastMessage(Guid.NewGuid(), message, ToastType.Info));
}
