using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

public class Account : BaseEntity
{
    // Azure AD integration properties
    public string? ExternalUserId { get; private set; }
    public string? Email { get; private set; }
    public AccountMode Mode { get; private set; } = AccountMode.Personal;
    public int? OwnerAccountId { get; private set; }
    public virtual Account? OwnerAccount { get; private set; }
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

    public Account CreateDemoAccount(string displayName)
    {
        if (Mode != AccountMode.Personal || !IsActive || Id <= 0)
            throw new InvalidOperationException("A demo requires an active, persisted personal owner.");
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new Account
        {
            Mode = AccountMode.Demo,
            OwnerAccountId = Id,
            OwnerAccount = this,
            DisplayName = displayName
        };
    }

    public bool CanAccessDemo(Account demo) =>
        Mode == AccountMode.Personal && IsActive && Id > 0 &&
        demo.Mode == AccountMode.Demo && demo.IsActive && demo.Id > 0 &&
        demo.Id != Id && demo.OwnerAccountId == Id && demo.ExternalUserId == null;
    
    public void UpdateUserInfo(string email, string displayName)
    {
        if (Mode != AccountMode.Personal)
            throw new InvalidOperationException("Demo accounts cannot have external user information.");
        Email = email ?? throw new ArgumentNullException(nameof(email));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        SetUpdatedAt();
    }
    
    public void RecordLogin()
    {
        if (Mode != AccountMode.Personal)
            throw new InvalidOperationException("Demo accounts cannot sign in independently.");
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
