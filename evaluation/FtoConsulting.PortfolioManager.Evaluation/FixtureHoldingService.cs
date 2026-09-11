using FtoConsulting.PortfolioManager.Application.DTOs;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Evaluation;

public sealed record PortfolioFixture(string Version, DateTimeOffset EffectiveTime, int AccountId, List<FixtureHoldingRow> Holdings);
public sealed record FixtureHoldingRow(DateOnly Date, string Ticker, string Name, decimal Units, decimal BoughtValue,
    decimal CurrentValue, decimal DailyProfitLoss = 0);

/// <summary>Controlled data boundary; the real holdings/analysis/comparison tools and PortfolioAnalysisService still run.</summary>
public sealed class FixtureHoldingService(PortfolioFixture fixture) : IHoldingService
{
    public Task<IEnumerable<Holding>> GetHoldingsByAccountAndDateAsync(int accountId, DateOnly date, CancellationToken cancellationToken = default)
        => ReadAsync(accountId, date, null, cancellationToken);
    public Task<IEnumerable<Holding>> GetHoldingsByAccountDateAndTickerAsync(int accountId, DateOnly date, string ticker, CancellationToken cancellationToken = default)
        => ReadAsync(accountId, date, ticker, cancellationToken);
    private Task<IEnumerable<Holding>> ReadAsync(int accountId, DateOnly date, string? ticker, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (accountId != fixture.AccountId) throw new InvalidOperationException("Account does not match evaluation fixture");
        if (!fixture.Holdings.Any(h => h.Date == date)) throw new InvalidOperationException($"No fixture for {date:yyyy-MM-dd}");
        return Task.FromResult<IEnumerable<Holding>>(fixture.Holdings.Where(h => h.Date == date &&
            (ticker == null || h.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase))).Select(h => new FixtureHolding(h)).ToArray());
    }
    public Task<HoldingAddResult> AddHoldingAsync(int portfolioId, AddHoldingRequest request, int accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<HoldingUpdateResult> UpdateHoldingUnitsAsync(int holdingId, decimal newUnits, int accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<HoldingDeleteResult> DeleteHoldingAsync(int holdingId, int accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    private sealed class FixtureHolding : Holding
    {
        private readonly Instrument _instrument;
        public override Instrument Instrument => _instrument;
        public FixtureHolding(FixtureHoldingRow row) : base(row.Date.ToDateTime(TimeOnly.MinValue), 1, 1, 1, row.Units, row.BoughtValue, row.CurrentValue)
        {
            _instrument = new Instrument(row.Name, row.Ticker, 1, currencyCode: "GBP");
            SetDailyProfitLoss(row.DailyProfitLoss, row.CurrentValue == row.DailyProfitLoss ? 0 : row.DailyProfitLoss / (row.CurrentValue - row.DailyProfitLoss) * 100);
        }
    }
}
