using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class IpReputationRepository : IIpReputationRepository
{
    private readonly SecurityDbContext _dbContext;

    public IpReputationRepository(SecurityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IpReputation?> GetByAddressAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.IpReputations
            .FirstOrDefaultAsync(x => x.IpAddress == address, cancellationToken);
    }

    public async Task AddAsync(
        IpReputation reputation,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.IpReputations.AddAsync(reputation, cancellationToken);
    }
}