using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

public class Account : BaseEntity
{
    // Azure AD integration properties
    public string ExternalUserId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }

    // Navigation properties
    public virtual ICollection<Portfolio> Portfolios { get; private set; } = [];

    // Private constructor for EF Core
    private Account() { }

    // Azure AD external user constructor
    public Account(string externalUserId, string email, string displayName)
    {
        ExternalUserId = externalUserId ?? throw new ArgumentNullException(nameof(externalUserId));
        Email = email ?? throw new ArgumentNullException(nameof(email));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }
    
    public void UpdateUserInfo(string email, string displayName)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        SetUpdatedAt();
    }
    
    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        SetUpdatedAt();
    }
    
    public void Deactivate()
    {
        IsActive = false;
        SetUpdatedAt();
    }
    
    public void Activate()
    {
        IsActive = true;
        SetUpdatedAt();
    }
}