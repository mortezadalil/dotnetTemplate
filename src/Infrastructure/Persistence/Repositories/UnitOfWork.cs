using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Unit of Work implementation for COMMAND operations (WRITE database).
/// Coordinates multiple repositories and manages transactions.
/// Used by command handlers to write to the source of truth.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly CommandDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(CommandDbContext context)
    {
        _context = context;

        // Initialize command repositories (write operations)
        Users = new Repository<User>(_context);
        Configs = new Repository<Config>(_context);
    }

    public IRepository<User> Users { get; }
    public IRepository<Config> Configs { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            throw new InvalidOperationException("No transaction to commit");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
