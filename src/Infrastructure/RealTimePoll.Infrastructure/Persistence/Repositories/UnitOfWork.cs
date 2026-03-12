using Microsoft.EntityFrameworkCore.Storage;
using RealTimePoll.Domain.Entities;
using RealTimePoll.Domain.Interfaces;
using RealTimePoll.Infrastructure.Persistence.Context;

namespace RealTimePoll.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    private IGenericRepository<Poll>? _polls;
    private IGenericRepository<PollOption>? _pollOptions;
    private IGenericRepository<Vote>? _votes;
    private IGenericRepository<RefreshToken>? _refreshTokens;

    public UnitOfWork(AppDbContext context) => _context = context;

    public IGenericRepository<Poll> Polls
        => _polls ??= new GenericRepository<Poll>(_context);

    public IGenericRepository<PollOption> PollOptions
        => _pollOptions ??= new GenericRepository<PollOption>(_context);

    public IGenericRepository<Vote> Votes
        => _votes ??= new GenericRepository<Vote>(_context);

    public IGenericRepository<RefreshToken> RefreshTokens
        => _refreshTokens ??= new GenericRepository<RefreshToken>(_context);

    public Task<int> SaveChangesAsync()
        => _context.SaveChangesAsync();

    public async Task BeginTransactionAsync()
        => _transaction = await _context.Database.BeginTransactionAsync();

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
