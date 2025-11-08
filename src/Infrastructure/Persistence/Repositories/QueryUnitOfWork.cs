using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Unit of Work implementation for QUERY operations (READ database).
/// Provides read-only access to optimized query database.
/// Used by query handlers for fast reads.
/// </summary>
public class QueryUnitOfWork : IUnitOfWork
{
    private readonly QueryDbContext _context;

    public QueryUnitOfWork(QueryDbContext context)
    {
        _context = context;

        // Initialize query repositories (read-only operations)
        Users = new QueryRepository<User>(_context);
        Configs = new QueryRepository<Config>(_context);
    }

    public IRepository<User> Users { get; }
    public IRepository<Config> Configs { get; }

    // SaveChanges not supported in Query DB from application code
    // Query DB is updated only via domain event handlers
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "SaveChangesAsync is not supported in QueryUnitOfWork. Query DB is updated via domain event handlers.");
    }

    // Transactions not supported in read-only context
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Transactions are not supported in QueryUnitOfWork.");
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Transactions are not supported in QueryUnitOfWork.");
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Transactions are not supported in QueryUnitOfWork.");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
