using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealTimePoll.Application.DTOs.Auth;
using RealTimePoll.Application.Interfaces;
using System.Security.Claims;

namespace RealTimePoll.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ForgotPasswordRequest> forgotValidator,
        IValidator<ResetPasswordRequest> resetValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
    }

    /// <summary>Yeni kullanıcı kaydı</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<object>.Fail(validation.Errors.Select(e => e.ErrorMessage)));

        var result = await _authService.RegisterAsync(request);
        return Ok(ApiResponse<AuthResponse>.Success(result, "Kayıt başarılı! E-posta doğrulama linki gönderildi."));
    }

    /// <summary>Kullanıcı girişi</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var validation = await _loginValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<object>.Fail(validation.Errors.Select(e => e.ErrorMessage)));

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _authService.LoginAsync(request, ipAddress, userAgent);
        return Ok(ApiResponse<AuthResponse>.Success(result, "Giriş başarılı."));
    }

    /// <summary>Access token yenile</summary>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.RefreshTokenAsync(request, ipAddress);
        return Ok(ApiResponse<AuthResponse>.Success(result));
    }

    /// <summary>Oturumu kapat</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RevokeTokenRequest request)
    {
        var userId = GetCurrentUserId();
        await _authService.RevokeTokenAsync(request, userId);
        return Ok(ApiResponse<object>.Success(null, "Çıkış yapıldı."));
    }

    /// <summary>Şifre sıfırlama maili gönder</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var validation = await _forgotValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<object>.Fail(validation.Errors.Select(e => e.ErrorMessage)));

        await _authService.ForgotPasswordAsync(request);
        return Ok(ApiResponse<object>.Success(null, "Şifre sıfırlama linki e-posta adresinize gönderildi."));
    }

    /// <summary>Şifreyi sıfırla</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var validation = await _resetValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<object>.Fail(validation.Errors.Select(e => e.ErrorMessage)));

        await _authService.ResetPasswordAsync(request);
        return Ok(ApiResponse<object>.Success(null, "Şifreniz başarıyla sıfırlandı."));
    }

    /// <summary>Şifre değiştir</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        await _authService.ChangePasswordAsync(request, GetCurrentUserId());
        return Ok(ApiResponse<object>.Success(null, "Şifreniz başarıyla değiştirildi."));
    }

    /// <summary>E-posta onayla</summary>
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        var success = await _authService.ConfirmEmailAsync(userId, token);
        if (!success)
            return BadRequest(ApiResponse<object>.Fail(new[] { "E-posta doğrulama başarısız veya link süresi dolmuş." }));

        return Ok(ApiResponse<object>.Success(null, "E-posta adresiniz doğrulandı!"));
    }

    /// <summary>Onay emailini tekrar gönder</summary>
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ForgotPasswordRequest request)
    {
        await _authService.ResendConfirmationEmailAsync(request.Email);
        return Ok(ApiResponse<object>.Success(null, "Onay e-postası tekrar gönderildi."));
    }

    /// <summary>Kendi profilini getir</summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _authService.GetProfileAsync(GetCurrentUserId());
        return Ok(ApiResponse<UserProfileResponse>.Success(result));
    }

    /// <summary>Profili güncelle</summary>
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var result = await _authService.UpdateProfileAsync(request, GetCurrentUserId());
        return Ok(ApiResponse<UserProfileResponse>.Success(result, "Profil güncellendi."));
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(claim ?? throw new UnauthorizedAccessException("Kimlik doğrulanamadı."));
    }
}

// ── Shared API Response Wrapper ───────────────────────────────────────────────

public record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public IEnumerable<string>? Errors { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Success(T? data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(IEnumerable<string> errors, string? message = null)
        => new() { Success = false, Errors = errors, Message = message };
}
