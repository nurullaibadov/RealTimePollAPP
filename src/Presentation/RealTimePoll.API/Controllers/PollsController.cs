using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RealTimePoll.API.Hubs;
using RealTimePoll.Application.DTOs.Poll;
using RealTimePoll.Application.Interfaces;
using System.Security.Claims;

namespace RealTimePoll.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PollsController : ControllerBase
{
    private readonly IPollService _pollService;
    private readonly IValidator<CreatePollRequest> _createValidator;
    private readonly IHubContext<PollHub> _hubContext;

    public PollsController(
        IPollService pollService,
        IValidator<CreatePollRequest> createValidator,
        IHubContext<PollHub> hubContext)
    {
        _pollService = pollService;
        _createValidator = createValidator;
        _hubContext = hubContext;
    }

    /// <summary>Tüm anketleri listele (sayfalı, filtreli)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PollSummaryResponse>>), 200)]
    public async Task<IActionResult> GetPolls([FromQuery] PollFilterRequest filter)
    {
        var result = await _pollService.GetPollsAsync(filter);
        return Ok(ApiResponse<PagedResult<PollSummaryResponse>>.Success(result));
    }

    /// <summary>Anket detayını getir</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PollResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPoll(Guid id)
    {
        var result = await _pollService.GetPollByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<object>.Fail(new[] { "Anket bulunamadı." }));

        return Ok(ApiResponse<PollResponse>.Success(result));
    }

    /// <summary>Kendi anketlerimi listele</summary>
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyPolls([FromQuery] PollFilterRequest filter)
    {
        var result = await _pollService.GetMyPollsAsync(GetCurrentUserId(), filter);
        return Ok(ApiResponse<PagedResult<PollSummaryResponse>>.Success(result));
    }

    /// <summary>Yeni anket oluştur</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PollResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> CreatePoll([FromBody] CreatePollRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<object>.Fail(validation.Errors.Select(e => e.ErrorMessage)));

        var result = await _pollService.CreatePollAsync(request, GetCurrentUserId());

        // Notify all clients about new poll
        await _hubContext.Clients.All.SendAsync("NewPollCreated", result);

        return CreatedAtAction(nameof(GetPoll), new { id = result.Id },
            ApiResponse<PollResponse>.Success(result, "Anket başarıyla oluşturuldu."));
    }

    /// <summary>Anketi güncelle</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdatePoll(Guid id, [FromBody] UpdatePollRequest request)
    {
        var result = await _pollService.UpdatePollAsync(id, request, GetCurrentUserId());
        await _hubContext.Clients.Group($"poll_{id}").SendAsync("PollUpdated", result);
        return Ok(ApiResponse<PollResponse>.Success(result, "Anket güncellendi."));
    }

    /// <summary>Anketi sil</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeletePoll(Guid id)
    {
        await _pollService.DeletePollAsync(id, GetCurrentUserId());
        await _hubContext.Clients.Group($"poll_{id}").SendAsync("PollDeleted", id);
        return Ok(ApiResponse<object>.Success(null, "Anket silindi."));
    }

    /// <summary>Anketi aktif et</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> ActivatePoll(Guid id)
    {
        await _pollService.ActivatePollAsync(id, GetCurrentUserId());
        await _hubContext.Clients.Group($"poll_{id}").SendAsync("PollStatusChanged", new { pollId = id, status = "Active" });
        return Ok(ApiResponse<object>.Success(null, "Anket aktif edildi."));
    }

    /// <summary>Anketi kapat</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> ClosePoll(Guid id)
    {
        await _pollService.ClosePollAsync(id, GetCurrentUserId());
        await _hubContext.Clients.Group($"poll_{id}").SendAsync("PollStatusChanged", new { pollId = id, status = "Closed" });
        return Ok(ApiResponse<object>.Success(null, "Anket kapatıldı."));
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(claim ?? throw new UnauthorizedAccessException("Kimlik doğrulanamadı."));
    }
}
