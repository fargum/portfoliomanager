using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

public class Platform : BaseEntity
{
    public string Name { get; private set; }

    // Navigation properties
    public virtual ICollection<Holding> Holdings { get; private set; } = new List<Holding>();

    // Private constructor for EF Core
    private Platform() { }

    public Platform(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public void UpdateName(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SetUpdatedAt();
    }
}