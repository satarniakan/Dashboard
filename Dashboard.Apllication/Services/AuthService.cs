using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Dashboard.Application.DTOs;
using Dashboard.Domain.Identity;

namespace Dashboard.Application.Services;

public interface IAuthService
{
    Task<PasswordLoginResult> LoginWithPasswordAsync(string email, string password);
    Task RequestOtpAsync(string phoneNumber);
    Task<OtpVerificationResult> VerifyOtpAsync(string phoneNumber, string code);
    Task LogoutAsync();
    Task<UserProfileDto?> GetProfileAsync(string userId);
    Task<ProfileUpdateResult> CompleteProfileAsync(string userId, string fullName, string? email, string? password, string? confirmPassword);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOtpService _otpService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOtpService otpService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _otpService = otpService;
        _logger = logger;
    }

    public async Task<PasswordLoginResult> LoginWithPasswordAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            _logger.LogWarning("Login failed: no user found for email {Email}", email);
            return new PasswordLoginResult(false);
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Login failed for {Email}: {Reason}", email,
                result.IsLockedOut ? "locked out" : result.IsNotAllowed ? "not allowed" : "invalid password");
        }

        return new PasswordLoginResult(result.Succeeded);
    }

    public async Task RequestOtpAsync(string phoneNumber)
    {
        await _otpService.GenerateAndSendOtpAsync(phoneNumber);
    }

    public async Task<OtpVerificationResult> VerifyOtpAsync(string phoneNumber, string code)
    {
        var isValid = await _otpService.VerifyOtpAsync(phoneNumber, code);

        if (!isValid)
        {
            return new OtpVerificationResult(false, false);
        }

        var user = await _userManager.FindByNameAsync(phoneNumber);
        var isNewUser = user is null;

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = phoneNumber,
                PhoneNumber = phoneNumber,
                PhoneNumberConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning("User creation failed for {PhoneNumber}: {Errors}",
                    phoneNumber, string.Join(" | ", createResult.Errors.Select(e => e.Description)));
                return new OtpVerificationResult(false, false);
            }
        }

        await _signInManager.SignInAsync(user, isPersistent: true);
        return new OtpVerificationResult(true, isNewUser);
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    public async Task<UserProfileDto?> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);

        return new UserProfileDto(user.PhoneNumber, user.FullName, user.Email, hasPassword);
    }

    public async Task<ProfileUpdateResult> CompleteProfileAsync(
        string userId, string fullName, string? email, string? password, string? confirmPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new ProfileUpdateResult(ProfileUpdateStatus.UserNotFound);
        }

        user.FullName = fullName;

        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser is not null && existingUser.Id != user.Id)
            {
                return new ProfileUpdateResult(ProfileUpdateStatus.EmailAlreadyExists);
            }

            var emailResult = await _userManager.SetEmailAsync(user, email);
            if (!emailResult.Succeeded)
            {
                _logger.LogWarning("SetEmail failed for user {UserId}: {Errors}",
                    userId, string.Join(" | ", emailResult.Errors.Select(e => e.Description)));
                return new ProfileUpdateResult(ProfileUpdateStatus.EmailUpdateFailed);
            }
        }
        if (!string.IsNullOrWhiteSpace(password))
        {
            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (hasPassword)
            {
                return new ProfileUpdateResult(ProfileUpdateStatus.PasswordAlreadySet);
            }

            if (password != confirmPassword)
            {
                return new ProfileUpdateResult(ProfileUpdateStatus.PasswordMismatch);
            }

            var passwordResult = await _userManager.AddPasswordAsync(user, password);
            if (!passwordResult.Succeeded)
            {
                _logger.LogWarning("AddPassword failed for user {UserId}: {Errors}",
                    userId, string.Join(" | ", passwordResult.Errors.Select(e => e.Description)));
                return new ProfileUpdateResult(ProfileUpdateStatus.PasswordUpdateFailed);
            }
        }

        await _userManager.UpdateAsync(user);

        return new ProfileUpdateResult(ProfileUpdateStatus.Success);
    }
}
