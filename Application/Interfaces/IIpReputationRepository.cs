using Domain.Entities;

namespace Application.Interfaces;

public interface IIpReputationRepository
{
    Task<IpReputation?> GetByAddressAsync(
        string address,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        IpReputation reputation,
        CancellationToken cancellationToken = default);
}