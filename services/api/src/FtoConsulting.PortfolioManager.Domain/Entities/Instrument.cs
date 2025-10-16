using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

public class Instrument : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Ticker { get; private set; } = string.Empty;
    public string? CurrencyCode { get; private set; }
    public string? QuoteUnit { get; private set; }
    public int InstrumentTypeId { get; private set; }

    // Navigation properties
    public virtual InstrumentType InstrumentType { get; private set; } = null!;
    public virtual ICollection<Holding> Holdings { get; private set; } = new List<Holding>();

    // Private constructor for EF Core
    private Instrument() { }

    public Instrument(string name, string ticker, int instrumentTypeId, string? description = null, string? currencyCode = null, string? quoteUnit = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Ticker = ticker ?? throw new ArgumentNullException(nameof(ticker));
        InstrumentTypeId = instrumentTypeId;
        Description = description;
        CurrencyCode = currencyCode;
        QuoteUnit = quoteUnit;
    }

    public void UpdateDetails(string name, string ticker, string? description = null, string? currencyCode = null, string? quoteUnit = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Ticker = ticker ?? throw new ArgumentNullException(nameof(ticker));
        Description = description;
        CurrencyCode = currencyCode;
        QuoteUnit = quoteUnit;
        SetUpdatedAt();
    }

    public void UpdateInstrumentType(int instrumentTypeId)
    {
        InstrumentTypeId = instrumentTypeId;
        SetUpdatedAt();
    }
}