using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

/// <summary>
/// Interface for the application database context.
/// This allows the Application layer to depend on an interface, not the concrete EF Core implementation.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Config> Configs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
