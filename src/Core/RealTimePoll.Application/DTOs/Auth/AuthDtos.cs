namespace RealTimePoll.Application.DTOs.Auth;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword
);

public record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false
);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmNewPassword
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);

public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken
);

public record RevokeTokenRequest(string RefreshToken);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    UserProfileResponse User
);

public record UserProfileResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string FullName,
    string? ProfileImageUrl,
    IList<string> Roles,
    DateTime CreatedAt
);

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? ProfileImageUrl
);
