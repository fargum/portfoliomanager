using FtoConsulting.PortfolioManager.Domain.Aggregates;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

public class Portfolio : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }

    // Navigation properties
    public virtual Account Account { get; private set; } = null!;
    public virtual ICollection<Holding> Holdings { get; private set; } = new List<Holding>();

    // Private constructor for EF Core
    private Portfolio() { }

    public Portfolio(string name, Guid accountId)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        AccountId = accountId;
    }

    public void UpdateName(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SetUpdatedAt();
    }

    public void AddHolding(Holding holding)
    {
        if (holding == null) throw new ArgumentNullException(nameof(holding));
        Holdings.Add(holding);
        SetUpdatedAt();
    }

    public void RemoveHolding(Holding holding)
    {
        if (holding == null) throw new ArgumentNullException(nameof(holding));
        Holdings.Remove(holding);
        SetUpdatedAt();
    }

    // Calculated properties
    public decimal TotalValue => Holdings.Sum(h => h.CurrentValue);
    public decimal TotalBoughtValue => Holdings.Sum(h => h.BoughtValue);
    public decimal TotalProfitLoss => Holdings.Sum(h => h.TotalProfitLoss);
    public decimal TotalDailyProfitLoss => Holdings.Sum(h => h.DailyProfitLoss);
    public decimal TotalProfitLossPercentage => TotalBoughtValue != 0 ? (TotalProfitLoss / TotalBoughtValue) * 100 : 0;
    public decimal TotalDailyProfitLossPercentage => TotalValue != 0 ? (TotalDailyProfitLoss / (TotalValue - TotalDailyProfitLoss)) * 100 : 0;
}