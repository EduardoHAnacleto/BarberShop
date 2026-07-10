using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace BarberShop.Application.Services;

public class LoyaltyService : ILoyaltyService
{
    private const int DefaultVisitsForReward = 10;

    private readonly IUnitOfWork _uow;
    private readonly int _visitsForReward;

    public LoyaltyService(IUnitOfWork uow, IConfiguration config)
    {
        _uow = uow;

        // Loyalty:VisitsForReward controls how many completed visits earn a
        // reward; misconfigured (missing/non-positive) values fall back to
        // the default rather than dividing by zero or producing a negative
        // "visits until reward".
        var configured = config.GetValue<int?>("Loyalty:VisitsForReward");
        _visitsForReward = configured is > 0 ? configured.Value : DefaultVisitsForReward;
    }

    public async Task<LoyaltyStatusDTO> GetStatusAsync(int customerId)
    {
        var appointments = await _uow.Appointments.GetByCustomer(customerId);
        var completedVisits = appointments?.Count(a => a.Status == Status.Completed) ?? 0;

        var remainder = completedVisits % _visitsForReward;
        var rewardReady = completedVisits > 0 && remainder == 0;

        return new LoyaltyStatusDTO
        {
            CompletedVisits = completedVisits,
            VisitsForReward = _visitsForReward,
            VisitsUntilReward = rewardReady ? 0 : _visitsForReward - remainder,
            RewardReady = rewardReady,
        };
    }
}
