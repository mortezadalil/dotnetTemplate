using Domain.Entities;

namespace Application.Common.Interfaces;

/// <summary>
/// Unit of Work pattern - manages transactions and coordinates repository operations.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Repository for User entities.
    /// </summary>
    IRepository<User> Users { get; }

    /// <summary>
    /// Repository for Config entities.
    /// </summary>
    IRepository<Config> Configs { get; }

    /// <summary>
    /// Saves all changes made in this unit of work.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
