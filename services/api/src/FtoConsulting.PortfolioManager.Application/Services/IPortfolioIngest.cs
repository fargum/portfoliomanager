using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for ingesting portfolio data including holdings and instruments
/// </summary>
public interface IPortfolioIngest
{
    /// <summary>
    /// Ingests a portfolio with its holdings, creating new instruments as needed
    /// </summary>
    /// <param name="portfolio">Portfolio with holdings to ingest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ingested portfolio with updated references</returns>
    Task<Portfolio> IngestPortfolioAsync(Portfolio portfolio, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests multiple portfolios with their holdings
    /// </summary>
    /// <param name="portfolios">Collection of portfolios to ingest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ingested portfolios with updated references</returns>
    Task<IEnumerable<Portfolio>> IngestPortfoliosAsync(IEnumerable<Portfolio> portfolios, CancellationToken cancellationToken = default);
}