namespace BarberShop.Application.DTOs;

// A customer's progress toward their next loyalty reward.
public class LoyaltyStatusDTO
{
    public int CompletedVisits { get; set; }
    public int VisitsForReward { get; set; }
    public int VisitsUntilReward { get; set; }
    public bool RewardReady { get; set; }
}
