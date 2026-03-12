using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealTimePoll.Application.DTOs.Poll;
using RealTimePoll.Application.Interfaces;
using RealTimePoll.Infrastructure.Identity;

namespace RealTimePoll.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IPollService _pollService;
    private readonly UserManager<AppIdentityUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public AdminController(
        IPollService pollService,
        UserManager<AppIdentityUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        _pollService = pollService;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    /// <summary>Dashboard istatistikleri</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var stats = await _pollService.GetDashboardStatsAsync();
        return Ok(ApiResponse<PollStatsResponse>.Success(stats));
    }

    /// <summary>Tüm kullanıcıları listele</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email!.Contains(search) ||
                                     u.FirstName.Contains(search) ||
                                     u.LastName.Contains(search));

        var total = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new List<object>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new
            {
                user.Id, user.FirstName, user.LastName, user.Email,
                user.IsActive, user.EmailConfirmed, user.CreatedAt,
                Roles = roles
            });
        }

        var paged = new PagedResult<object>(result, total, page, pageSize,
            (int)Math.Ceiling((double)total / pageSize));

        return Ok(ApiResponse<PagedResult<object>>.Success(paged));
    }

    /// <summary>Kullanıcıyı aktif/pasif yap</summary>
    [HttpPut("users/{userId:guid}/toggle-status")]
    public async Task<IActionResult> ToggleUserStatus(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return NotFound(ApiResponse<object>.Fail(new[] { "Kullanıcı bulunamadı." }));

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        return Ok(ApiResponse<object>.Success(null,
            user.IsActive ? "Kullanıcı aktif edildi." : "Kullanıcı pasif edildi."));
    }

    /// <summary>Kullanıcıya rol ata</summary>
    [HttpPost("users/{userId:guid}/roles")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> AssignRole(Guid userId, [FromBody] AssignRoleRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return NotFound(ApiResponse<object>.Fail(new[] { "Kullanıcı bulunamadı." }));

        if (!await _roleManager.RoleExistsAsync(request.Role))
            await _roleManager.CreateAsync(new IdentityRole<Guid>(request.Role));

        if (!await _userManager.IsInRoleAsync(user, request.Role))
            await _userManager.AddToRoleAsync(user, request.Role);

        return Ok(ApiResponse<object>.Success(null, $"{request.Role} rolü atandı."));
    }

    /// <summary>Kullanıcıdan rol al</summary>
    [HttpDelete("users/{userId:guid}/roles/{role}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> RemoveRole(Guid userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return NotFound(ApiResponse<object>.Fail(new[] { "Kullanıcı bulunamadı." }));

        await _userManager.RemoveFromRoleAsync(user, role);
        return Ok(ApiResponse<object>.Success(null, $"{role} rolü kaldırıldı."));
    }

    /// <summary>Tüm anketleri yönet</summary>
    [HttpGet("polls")]
    public async Task<IActionResult> GetAllPolls([FromQuery] PollFilterRequest filter)
    {
        var result = await _pollService.GetPollsAsync(filter);
        return Ok(ApiResponse<PagedResult<PollSummaryResponse>>.Success(result));
    }
}

public record AssignRoleRequest(string Role);
