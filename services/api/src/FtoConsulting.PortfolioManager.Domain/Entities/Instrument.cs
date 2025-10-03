using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

public class Instrument : BaseEntity
{
    public string ISIN { get; private set; } = string.Empty;
    public string? SEDOL { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Ticker { get; private set; }
    public Guid InstrumentTypeId { get; private set; }

    // Navigation properties
    public virtual InstrumentType InstrumentType { get; private set; } = null!;
    public virtual ICollection<Holding> Holdings { get; private set; } = new List<Holding>();

    // Private constructor for EF Core
    private Instrument() { }

    public Instrument(string isin, string name, Guid instrumentTypeId, string? sedol = null, string? description = null, string? ticker = null)
    {
        ISIN = isin ?? throw new ArgumentNullException(nameof(isin));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        InstrumentTypeId = instrumentTypeId;
        SEDOL = sedol;
        Description = description;
        Ticker = ticker;
    }

    public void UpdateDetails(string isin, string name, string? sedol = null, string? description = null, string? ticker = null)
    {
        ISIN = isin ?? throw new ArgumentNullException(nameof(isin));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SEDOL = sedol;
        Description = description;
        Ticker = ticker;
        SetUpdatedAt();
    }

    public void UpdateInstrumentType(Guid instrumentTypeId)
    {
        InstrumentTypeId = instrumentTypeId;
        SetUpdatedAt();
    }
}