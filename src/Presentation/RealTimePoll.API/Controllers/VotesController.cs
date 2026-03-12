using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RealTimePoll.API.Hubs;
using RealTimePoll.Application.DTOs.Vote;
using RealTimePoll.Application.Interfaces;
using System.Security.Claims;

namespace RealTimePoll.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class VotesController : ControllerBase
{
    private readonly IVoteService _voteService;
    private readonly IValidator<CastVoteRequest> _voteValidator;
    private readonly IHubContext<PollHub> _hubContext;

    public VotesController(
        IVoteService voteService,
        IValidator<CastVoteRequest> voteValidator,
        IHubContext<PollHub> hubContext)
    {
        _voteService = voteService;
        _voteValidator = voteValidator;
        _hubContext = hubContext;
    }

    /// <summary>Oy kullan (anonim veya giriş yapılmış)</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<VoteResultResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> CastVote([FromBody] CastVoteRequest request)
    {
        var validation = await _voteValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<VoteResultResponse>.Fail(validation.Errors.Select(e => e.ErrorMessage)));

        var userId = GetCurrentUserIdOrNull();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var sessionId = HttpContext.Session.Id;

        var result = await _voteService.CastVoteAsync(request, userId, ipAddress, sessionId);

        // Broadcast real-time update to all poll viewers
        await _hubContext.Clients
            .Group($"poll_{request.PollId}")
            .SendAsync("VoteUpdate", result);

        return Ok(ApiResponse<VoteResultResponse>.Success(result, "Oyunuz başarıyla kaydedildi!"));
    }

    /// <summary>Anket sonuçlarını getir</summary>
    [HttpGet("{pollId:guid}/results")]
    [ProducesResponseType(typeof(ApiResponse<VoteResultResponse>), 200)]
    public async Task<IActionResult> GetResults(Guid pollId)
    {
        var result = await _voteService.GetResultsAsync(pollId);
        return Ok(ApiResponse<VoteResultResponse>.Success(result));
    }

    /// <summary>Kullanıcının ankette oy kullanıp kullanmadığını kontrol et</summary>
    [HttpGet("{pollId:guid}/has-voted")]
    public async Task<IActionResult> HasVoted(Guid pollId)
    {
        var userId = GetCurrentUserIdOrNull();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var hasVoted = await _voteService.HasUserVotedAsync(pollId, userId, ipAddress);
        return Ok(ApiResponse<bool>.Success(hasVoted));
    }

    private Guid? GetCurrentUserIdOrNull()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }
}
