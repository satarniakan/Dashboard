namespace Dashboard.Application.DTOs;

public record UserListItemDto(
    string UserId,
    string? PhoneNumber,
    string? FullName,
    string? Email,
    string? CurrentRole);