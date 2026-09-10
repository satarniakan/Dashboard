namespace Dashboard.Application.DTOs;

public record PasswordLoginResult(bool Succeeded);

public record OtpVerificationResult(bool Succeeded, bool IsNewUser);

public enum ProfileUpdateStatus
{
    Success,
    UserNotFound,
    EmailAlreadyExists,
    EmailUpdateFailed,
    PasswordAlreadySet,
    PasswordMismatch,
    PasswordUpdateFailed
}
public record ProfileUpdateResult(ProfileUpdateStatus Status);

public record UserProfileDto(
    string? PhoneNumber,
    string? FullName,
    string? Email,
    bool HasPassword);
