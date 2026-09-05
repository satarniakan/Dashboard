// Dashboard.Domain/Interfaces/ISmsSender.cs
namespace Dashboard.Domain.Interfaces;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message);
}