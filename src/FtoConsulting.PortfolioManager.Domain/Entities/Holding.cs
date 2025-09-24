using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

public class Holding : BaseEntity
{
    public DateTime ValuationDate { get; private set; }
    public Guid InstrumentId { get; private set; }
    public Guid PlatformId { get; private set; }
    public Guid PortfolioId { get; private set; }
    public decimal UnitAmount { get; private set; }
    public decimal BoughtValue { get; private set; }
    public decimal CurrentValue { get; private set; }

    // Navigation properties
    public virtual Instrument Instrument { get; private set; }
    public virtual Platform Platform { get; private set; }
    public virtual Portfolio Portfolio { get; private set; }

    // Private constructor for EF Core
    private Holding() { }

    public Holding(DateTime valuationDate, Guid instrumentId, Guid platformId, Guid portfolioId, 
                   decimal unitAmount, decimal boughtValue, decimal currentValue)
    {
        ValuationDate = valuationDate;
        InstrumentId = instrumentId;
        PlatformId = platformId;
        PortfolioId = portfolioId;
        UnitAmount = unitAmount;
        BoughtValue = boughtValue;
        CurrentValue = currentValue;
    }

    public void UpdateValuation(DateTime valuationDate, decimal currentValue)
    {
        ValuationDate = valuationDate;
        CurrentValue = currentValue;
        SetUpdatedAt();
    }

    public void UpdatePosition(decimal unitAmount, decimal boughtValue)
    {
        UnitAmount = unitAmount;
        BoughtValue = boughtValue;
        SetUpdatedAt();
    }

    public void UpdateCurrentValue(decimal currentValue)
    {
        CurrentValue = currentValue;
        SetUpdatedAt();
    }

    // Calculated properties
    public decimal TotalProfitLoss => CurrentValue - BoughtValue;
    public decimal TotalProfitLossPercentage => BoughtValue != 0 ? (TotalProfitLoss / BoughtValue) * 100 : 0;
    
    // Note: Daily P&L requires previous day's value - this would typically be calculated 
    // by comparing with previous holding record or storing previous value
    public decimal DailyProfitLoss { get; private set; }
    public decimal DailyProfitLossPercentage { get; private set; }

    public void SetDailyProfitLoss(decimal dailyProfitLoss, decimal dailyProfitLossPercentage)
    {
        DailyProfitLoss = dailyProfitLoss;
        DailyProfitLossPercentage = dailyProfitLossPercentage;
        SetUpdatedAt();
    }
}