using RealTimePoll.Domain.Entities;
using System.Linq.Expressions;

namespace RealTimePoll.Domain.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
    void SoftDelete(T entity);
}

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<Poll> Polls { get; }
    IGenericRepository<PollOption> PollOptions { get; }
    IGenericRepository<Vote> Votes { get; }
    IGenericRepository<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
