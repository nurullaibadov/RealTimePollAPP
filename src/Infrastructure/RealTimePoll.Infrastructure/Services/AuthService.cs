using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealTimePoll.Application.DTOs.Auth;
using RealTimePoll.Application.Interfaces;
using RealTimePoll.Infrastructure.Identity;
using RealTimePoll.Infrastructure.Persistence.Context;

namespace RealTimePoll.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<AppIdentityUser> _userManager;
    private readonly SignInManager<AppIdentityUser> _signInManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly TokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppIdentityUser> userManager,
        SignInManager<AppIdentityUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        TokenService tokenService,
        IEmailService emailService,
        AppDbContext context,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _context = context;
        _config = config;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            throw new InvalidOperationException("Bu e-posta adresi zaten kayıtlı.");

        var user = new AppIdentityUser
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

        if (!await _roleManager.RoleExistsAsync("User"))
            await _roleManager.CreateAsync(new IdentityRole<Guid>("User"));

        await _userManager.AddToRoleAsync(user, "User");

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var baseUrl = _config["App:BaseUrl"] ?? "http://localhost:5000";
        var confirmLink = $"{baseUrl}/api/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        try { await _emailService.SendEmailConfirmationAsync(user.Email!, user.FullName, confirmLink); }
        catch (Exception ex) { _logger.LogWarning(ex, "Email gönderilemedi: {Email}", user.Email); }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, "register", "register");

        return new AuthResponse(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(60), MapToProfile(user, roles));
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
                throw new UnauthorizedAccessException("Hesabınız geçici olarak kilitlendi.");
            throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, ipAddress, userAgent);
        var expiryMinutes = Convert.ToDouble(_config["Jwt:AccessTokenExpiryMinutes"] ?? "60");

        _logger.LogInformation("User logged in: {Email} from {IP}", user.Email, ipAddress);

        return new AuthResponse(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(expiryMinutes), MapToProfile(user, roles));
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress)
    {
        var userId = _tokenService.GetUserIdFromExpiredToken(request.AccessToken);
        if (userId == null)
            throw new UnauthorizedAccessException("Geçersiz access token.");

        var user = await _userManager.FindByIdAsync(userId.ToString()!);
        if (user == null)
            throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && t.UserId == user.Id);

        if (storedToken == null || !storedToken.IsActive)
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş refresh token.");

        storedToken.IsRevoked = true;
        storedToken.RevokedReason = "Replaced by new token";
        _context.RefreshTokens.Update(storedToken);

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(user, ipAddress, "refresh");
        await _context.SaveChangesAsync();

        var expiryMinutes = Convert.ToDouble(_config["Jwt:AccessTokenExpiryMinutes"] ?? "60");
        return new AuthResponse(newAccessToken, newRefreshToken, DateTime.UtcNow.AddMinutes(expiryMinutes), MapToProfile(user, roles));
    }

    public async Task RevokeTokenAsync(RevokeTokenRequest request, Guid userId)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && t.UserId == userId);

        if (token == null || !token.IsActive)
            throw new InvalidOperationException("Token bulunamadı veya zaten geçersiz.");

        token.IsRevoked = true;
        token.RevokedReason = "User logout";
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync();
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var frontendUrl = _config["App:FrontendUrl"] ?? "http://localhost:3000";
        var resetLink = $"{frontendUrl}/reset-password?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";

        try { await _emailService.SendPasswordResetAsync(user.Email!, user.FullName, resetLink); }
        catch (Exception ex) { _logger.LogError(ex, "Password reset email failed for {Email}", user.Email); }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Şifre sıfırlama başarısız: {errors}");
        }

        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync();

        foreach (var t in tokens)
        {
            t.IsRevoked = true;
            t.RevokedReason = "Password reset";
        }
        await _context.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Şifre değiştirme başarısız: {errors}");
        }
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var roles = await _userManager.GetRolesAsync(user);
        return MapToProfile(user, roles);
    }

    public async Task<UserProfileResponse> UpdateProfileAsync(UpdateProfileRequest request, Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

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

    private static UserProfileResponse MapToProfile(AppIdentityUser user, IList<string> roles)
        => new(user.Id, user.FirstName, user.LastName, user.Email ?? "",
               user.FullName, user.ProfileImageUrl, roles, user.CreatedAt);
}
