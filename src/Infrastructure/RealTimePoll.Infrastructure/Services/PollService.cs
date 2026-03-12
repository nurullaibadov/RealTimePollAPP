using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RealTimePoll.Application.DTOs.Poll;
using RealTimePoll.Application.Interfaces;
using RealTimePoll.Domain.Entities;
using RealTimePoll.Domain.Enums;
using RealTimePoll.Domain.Interfaces;
using RealTimePoll.Infrastructure.Persistence.Context;

namespace RealTimePoll.Infrastructure.Services;

public class PollService : IPollService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;

    public PollService(IUnitOfWork uow, AppDbContext context, UserManager<AppUser> userManager)
    {
        _uow = uow;
        _context = context;
        _userManager = userManager;
    }

    public async Task<PollResponse> CreatePollAsync(CreatePollRequest request, Guid userId)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            var poll = new Poll
            {
                Title = request.Title,
                Description = request.Description,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                AllowMultipleVotes = request.AllowMultipleVotes,
                IsAnonymous = request.IsAnonymous,
                ThumbnailUrl = request.ThumbnailUrl,
                CreatedByUserId = userId,
                Status = request.StartsAt <= DateTime.UtcNow ? PollStatus.Active : PollStatus.Draft,
                IsActive = request.StartsAt <= DateTime.UtcNow
            };

            await _uow.Polls.AddAsync(poll);
            await _uow.SaveChangesAsync();

            var options = request.Options.Select((o, i) => new PollOption
            {
                PollId = poll.Id,
                Text = o.Text,
                ImageUrl = o.ImageUrl,
                OrderIndex = o.OrderIndex > 0 ? o.OrderIndex : i
            }).ToList();

            await _uow.PollOptions.AddRangeAsync(options);
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();

            return await GetPollByIdAsync(poll.Id) ?? throw new Exception("Poll created but not found");
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<PollResponse> UpdatePollAsync(Guid pollId, UpdatePollRequest request, Guid userId)
    {
        var poll = await _uow.Polls.GetByIdAsync(pollId)
            ?? throw new KeyNotFoundException("Anket bulunamadı.");

        if (poll.CreatedByUserId != userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
            if (!roles.Contains("Admin") && !roles.Contains("SuperAdmin"))
                throw new UnauthorizedAccessException("Bu anketi düzenleme yetkiniz yok.");
        }

        if (poll.Status == PollStatus.Closed)
            throw new InvalidOperationException("Kapalı anket düzenlenemez.");

        poll.Title = request.Title;
        poll.Description = request.Description;
        poll.StartsAt = request.StartsAt;
        poll.EndsAt = request.EndsAt;
        poll.AllowMultipleVotes = request.AllowMultipleVotes;
        poll.IsAnonymous = request.IsAnonymous;
        poll.ThumbnailUrl = request.ThumbnailUrl;

        _uow.Polls.Update(poll);
        await _uow.SaveChangesAsync();

        return await GetPollByIdAsync(pollId) ?? throw new Exception("Poll updated but not found");
    }

    public async Task DeletePollAsync(Guid pollId, Guid userId)
    {
        var poll = await _uow.Polls.GetByIdAsync(pollId)
            ?? throw new KeyNotFoundException("Anket bulunamadı.");

        if (poll.CreatedByUserId != userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
            if (!roles.Contains("Admin") && !roles.Contains("SuperAdmin"))
                throw new UnauthorizedAccessException("Bu anketi silme yetkiniz yok.");
        }

        _uow.Polls.SoftDelete(poll);
        await _uow.SaveChangesAsync();
    }

    public async Task<PollResponse?> GetPollByIdAsync(Guid pollId)
    {
        var poll = await _context.Polls
            .Include(p => p.Options)
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == pollId && !p.IsDeleted);

        return poll == null ? null : MapToResponse(poll);
    }

    public async Task<PagedResult<PollSummaryResponse>> GetPollsAsync(PollFilterRequest filter)
    {
        var query = _context.Polls
            .Include(p => p.Options)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = query.Where(p => p.Title.Contains(filter.Search) ||
                                     (p.Description != null && p.Description.Contains(filter.Search)));

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(p => p.IsActive == filter.IsActive.Value);

        // Auto-update status based on time
        var now = DateTime.UtcNow;
        query = query.Where(p => !p.IsDeleted);

        var total = await query.CountAsync();

        query = filter.SortBy.ToLower() switch
        {
            "title" => filter.SortDesc ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),
            "votes" => filter.SortDesc ? query.OrderByDescending(p => p.TotalVotes) : query.OrderBy(p => p.TotalVotes),
            "endsat" => filter.SortDesc ? query.OrderByDescending(p => p.EndsAt) : query.OrderBy(p => p.EndsAt),
            _ => filter.SortDesc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
        };

        var polls = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var items = polls.Select(MapToSummary);
        return new PagedResult<PollSummaryResponse>(items, total, filter.Page, filter.PageSize,
            (int)Math.Ceiling((double)total / filter.PageSize));
    }

    public async Task<PagedResult<PollSummaryResponse>> GetMyPollsAsync(Guid userId, PollFilterRequest filter)
    {
        var query = _context.Polls
            .Include(p => p.Options)
            .Where(p => !p.IsDeleted && p.CreatedByUserId == userId)
            .AsQueryable();

        var total = await query.CountAsync();
        var polls = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var items = polls.Select(MapToSummary);
        return new PagedResult<PollSummaryResponse>(items, total, filter.Page, filter.PageSize,
            (int)Math.Ceiling((double)total / filter.PageSize));
    }

    public async Task ActivatePollAsync(Guid pollId, Guid userId)
    {
        var poll = await _uow.Polls.GetByIdAsync(pollId)
            ?? throw new KeyNotFoundException("Anket bulunamadı.");

        poll.Status = PollStatus.Active;
        poll.IsActive = true;
        _uow.Polls.Update(poll);
        await _uow.SaveChangesAsync();
    }

    public async Task ClosePollAsync(Guid pollId, Guid userId)
    {
        var poll = await _uow.Polls.GetByIdAsync(pollId)
            ?? throw new KeyNotFoundException("Anket bulunamadı.");

        poll.Status = PollStatus.Closed;
        poll.IsActive = false;
        _uow.Polls.Update(poll);
        await _uow.SaveChangesAsync();
    }

    public async Task<PollStatsResponse> GetDashboardStatsAsync()
    {
        var totalPolls = await _context.Polls.CountAsync(p => !p.IsDeleted);
        var activePolls = await _context.Polls.CountAsync(p => !p.IsDeleted && p.IsActive);
        var totalVotes = await _context.Votes.CountAsync(v => !v.IsDeleted);
        var totalUsers = await _context.Users.CountAsync();

        var recentPolls = await _context.Polls
            .Include(p => p.Options)
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync();

        return new PollStatsResponse(
            totalPolls, activePolls, totalVotes, totalUsers,
            recentPolls.Select(MapToSummary).ToList()
        );
    }

    private static PollResponse MapToResponse(Poll poll)
    {
        var options = poll.Options
            .Where(o => !o.IsDeleted)
            .OrderBy(o => o.OrderIndex)
            .Select(o => new PollOptionResponse(
                o.Id, o.Text, o.ImageUrl, o.VoteCount, o.OrderIndex,
                poll.TotalVotes > 0 ? Math.Round((double)o.VoteCount / poll.TotalVotes * 100, 1) : 0
            )).ToList();

        return new PollResponse(
            poll.Id, poll.Title, poll.Description,
            poll.StartsAt, poll.EndsAt,
            poll.IsActive, poll.AllowMultipleVotes, poll.IsAnonymous,
            poll.Status, poll.ThumbnailUrl, poll.TotalVotes,
            poll.CreatedByUser?.FullName ?? "Unknown",
            poll.CreatedAt, options
        );
    }

    private static PollSummaryResponse MapToSummary(Poll poll)
        => new(poll.Id, poll.Title, poll.Status, poll.IsActive,
               poll.TotalVotes, poll.EndsAt, poll.CreatedAt,
               poll.Options.Count(o => !o.IsDeleted));
}
