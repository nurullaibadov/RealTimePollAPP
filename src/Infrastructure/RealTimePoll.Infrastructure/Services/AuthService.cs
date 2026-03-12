using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealTimePoll.Application.DTOs.Auth;
using RealTimePoll.Application.Interfaces;
using RealTimePoll.Domain.Entities;
using RealTimePoll.Domain.Interfaces;

namespace RealTimePoll.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ITokenService tokenService,
        IEmailService emailService,
        IUnitOfWork uow,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _uow = uow;
        _config = config;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            throw new InvalidOperationException("Bu e-posta adresi zaten kayıtlı.");

        var user = new AppUser
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = request.Email,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Kayıt başarısız: {errors}");
        }

        // Assign default role
        if (!await _roleManager.RoleExistsAsync("User"))
            await _roleManager.CreateAsync(new IdentityRole<Guid>("User"));

        await _userManager.AddToRoleAsync(user, "User");

        // Send confirmation email
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var baseUrl = _config["App:BaseUrl"] ?? "http://localhost:5000";
        var confirmLink = $"{baseUrl}/api/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        try { await _emailService.SendEmailConfirmationAsync(user.Email!, user.FullName, confirmLink); }
        catch (Exception ex) { _logger.LogWarning(ex, "Email gönderilemedi: {Email}", user.Email); }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, "register", "register");

        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(60),
            MapToProfile(user, roles)
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Hesabınız devre dışı bırakılmıştır.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
                throw new UnauthorizedAccessException("Hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin.");
            throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, ipAddress, userAgent);

        _logger.LogInformation("User logged in: {Email} from {IP}", user.Email, ipAddress);

        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:AccessTokenExpiryMinutes"] ?? "60")),
            MapToProfile(user, roles)
        );
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress)
    {
        var userId = _tokenService.GetUserIdFromExpiredToken(request.AccessToken);
        if (userId == null)
            throw new UnauthorizedAccessException("Geçersiz access token.");

        var user = await _userManager.FindByIdAsync(userId.ToString()!);
        if (user == null)
            throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        var storedToken = await _uow.RefreshTokens.FirstOrDefaultAsync(
            t => t.Token == request.RefreshToken && t.UserId == user.Id);

        if (storedToken == null || !storedToken.IsActive)
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş refresh token.");

        // Revoke old token
        storedToken.IsRevoked = true;
        storedToken.RevokedReason = "Replaced by new token";
        _uow.RefreshTokens.Update(storedToken);

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(user, ipAddress, "refresh");
        await _uow.SaveChangesAsync();

        return new AuthResponse(
            newAccessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:AccessTokenExpiryMinutes"] ?? "60")),
            MapToProfile(user, roles)
        );
    }

    public async Task RevokeTokenAsync(RevokeTokenRequest request, Guid userId)
    {
        var token = await _uow.RefreshTokens.FirstOrDefaultAsync(
            t => t.Token == request.RefreshToken && t.UserId == userId);

        if (token == null || !token.IsActive)
            throw new InvalidOperationException("Token bulunamadı veya zaten geçersiz.");

        token.IsRevoked = true;
        token.RevokedReason = "User logout";
        _uow.RefreshTokens.Update(token);
        await _uow.SaveChangesAsync();
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return; // Security: don't reveal if email exists

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var frontendUrl = _config["App:FrontendUrl"] ?? "http://localhost:3000";
        var resetLink = $"{frontendUrl}/reset-password?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";

        try { await _emailService.SendPasswordResetAsync(user.Email!, user.FullName, resetLink); }
        catch (Exception ex) { _logger.LogError(ex, "Password reset email failed for {Email}", user.Email); }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Şifre sıfırlama başarısız: {errors}");
        }

        // Revoke all refresh tokens on password reset
        var tokens = await _uow.RefreshTokens.FindAsync(t => t.UserId == user.Id && !t.IsRevoked);
        foreach (var t in tokens)
        {
            t.IsRevoked = true;
            t.RevokedReason = "Password reset";
            _uow.RefreshTokens.Update(t);
        }
        await _uow.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Şifre değiştirme başarısız: {errors}");
        }
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var roles = await _userManager.GetRolesAsync(user);
        return MapToProfile(user, roles);
    }

    public async Task<UserProfileResponse> UpdateProfileAsync(UpdateProfileRequest request, Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            throw new InvalidOperationException("Kullanıcı bulunamadı.");

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.ProfileImageUrl = request.ProfileImageUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        return MapToProfile(user, roles);
    }

    public async Task<bool> ConfirmEmailAsync(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            try { await _emailService.SendWelcomeEmailAsync(user.Email!, user.FullName); }
            catch { /* ignore */ }
        }
        return result.Succeeded;
    }

    public async Task ResendConfirmationEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.EmailConfirmed) return;

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var baseUrl = _config["App:BaseUrl"] ?? "http://localhost:5000";
        var confirmLink = $"{baseUrl}/api/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await _emailService.SendEmailConfirmationAsync(user.Email!, user.FullName, confirmLink);
    }

    private static UserProfileResponse MapToProfile(AppUser user, IList<string> roles)
        => new(user.Id, user.FirstName, user.LastName, user.Email ?? "",
               user.FullName, user.ProfileImageUrl, roles, user.CreatedAt);
}
