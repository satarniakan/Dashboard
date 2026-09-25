namespace Dashboard.Application.DTOs;

public record UserListItemDto(
    string UserId,
    string? PhoneNumber,
    string? FullName,
    string? Email,
    List<string> Roles);