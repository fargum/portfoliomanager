using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

public class InstrumentType : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    // Navigation properties
    public virtual ICollection<Instrument> Instruments { get; private set; } = new List<Instrument>();

    // Private constructor for EF Core
    private InstrumentType() { }

    public InstrumentType(string name, string? description = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
    }

    public void UpdateDetails(string name, string? description = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
        SetUpdatedAt();
    }
}