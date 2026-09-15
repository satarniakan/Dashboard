namespace Dashboard.Web.Services;

public enum ToastType { Success, Error, Info }

public record ToastMessage(string Text, ToastType Type);

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message) => OnShow?.Invoke(new ToastMessage(message, ToastType.Success));
    public void ShowError(string message) => OnShow?.Invoke(new ToastMessage(message, ToastType.Error));
    public void ShowInfo(string message) => OnShow?.Invoke(new ToastMessage(message, ToastType.Info));
}