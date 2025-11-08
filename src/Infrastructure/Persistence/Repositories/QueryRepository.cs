using Application.Common.Interfaces;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic repository implementation for QUERY operations (READ database).
/// Used by query handlers to read from optimized read database.
/// This repository is READ-ONLY - writes are not allowed.
/// </summary>
public class QueryRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly QueryDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public QueryRepository(QueryDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // READ operations - optimized for queries
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking() // No tracking for read-only operations
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();

        if (filter != null)
        {
            query = query.Where(filter);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public virtual async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();

        if (filter != null)
        {
            query = query.Where(filter);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public virtual async Task<T?> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AsNoTracking().AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking();

        return predicate == null
            ? await query.CountAsync(cancellationToken)
            : await query.CountAsync(predicate, cancellationToken);
    }

    // WRITE operations - NOT SUPPORTED in Query DB
    // These throw exceptions if accidentally used in query handlers
    public virtual Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "AddAsync is not supported in QueryRepository. Use CommandRepository for write operations.");
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "UpdateAsync is not supported in QueryRepository. Use CommandRepository for write operations.");
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "DeleteAsync is not supported in QueryRepository. Use CommandRepository for write operations.");
    }

    public virtual Task HardDeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "HardDeleteAsync is not supported in QueryRepository. Use CommandRepository for write operations.");
    }
}
