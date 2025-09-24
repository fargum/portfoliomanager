using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

public class Account : BaseEntity
{
    public string UserName { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<Portfolio> Portfolios { get; private set; } = new List<Portfolio>();

    // Private constructor for EF Core
    private Account() { }

    public Account(string userName, string password)
    {
        UserName = userName ?? throw new ArgumentNullException(nameof(userName));
        Password = password ?? throw new ArgumentNullException(nameof(password));
    }

    public void UpdateCredentials(string userName, string password)
    {
        UserName = userName ?? throw new ArgumentNullException(nameof(userName));
        Password = password ?? throw new ArgumentNullException(nameof(password));
        SetUpdatedAt();
    }
}