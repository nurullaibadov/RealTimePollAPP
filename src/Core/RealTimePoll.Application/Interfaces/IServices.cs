using RealTimePoll.Application.DTOs.Auth;
using RealTimePoll.Application.DTOs.Poll;
using RealTimePoll.Application.DTOs.Vote;
using RealTimePoll.Domain.Entities;

namespace RealTimePoll.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress);
    Task RevokeTokenAsync(RevokeTokenRequest request, Guid userId);
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task ChangePasswordAsync(ChangePasswordRequest request, Guid userId);
    Task<UserProfileResponse> GetProfileAsync(Guid userId);
    Task<UserProfileResponse> UpdateProfileAsync(UpdateProfileRequest request, Guid userId);
    Task<bool> ConfirmEmailAsync(string userId, string token);
    Task ResendConfirmationEmailAsync(string email);
}

public interface IPollService
{
    Task<PollResponse> CreatePollAsync(CreatePollRequest request, Guid userId);
    Task<PollResponse> UpdatePollAsync(Guid pollId, UpdatePollRequest request, Guid userId);
    Task DeletePollAsync(Guid pollId, Guid userId);
    Task<PollResponse?> GetPollByIdAsync(Guid pollId);
    Task<PagedResult<PollSummaryResponse>> GetPollsAsync(PollFilterRequest filter);
    Task<PagedResult<PollSummaryResponse>> GetMyPollsAsync(Guid userId, PollFilterRequest filter);
    Task ActivatePollAsync(Guid pollId, Guid userId);
    Task ClosePollAsync(Guid pollId, Guid userId);
    Task<PollStatsResponse> GetDashboardStatsAsync();
}

public interface IVoteService
{
    Task<VoteResultResponse> CastVoteAsync(CastVoteRequest request, Guid? userId, string? ipAddress, string? sessionId);
    Task<VoteResultResponse> GetResultsAsync(Guid pollId);
    Task<bool> HasUserVotedAsync(Guid pollId, Guid? userId, string? ipAddress);
}

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string toEmail, string userName, string confirmationLink);
    Task SendPasswordResetAsync(string toEmail, string userName, string resetLink);
    Task SendWelcomeEmailAsync(string toEmail, string userName);
    Task SendPollResultsAsync(string toEmail, string userName, VoteResultResponse results);
}

public interface ITokenService
{
    string GenerateAccessToken(AppUser user, IList<string> roles);
    Task<string> GenerateRefreshTokenAsync(AppUser user, string ipAddress, string userAgent);
    Guid? GetUserIdFromExpiredToken(string token);
    bool ValidateToken(string token);
}
