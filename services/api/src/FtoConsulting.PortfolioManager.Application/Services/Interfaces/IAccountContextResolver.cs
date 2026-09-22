using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

public sealed record AccountContext(int PersonalAccountId, int AccountId, AccountMode Mode);

public interface IAccountContextResolver
{
    // The caller selects only a mode; ownership comes from the authenticated identity.
    Task<AccountContext> ResolveAsync(AccountMode mode = AccountMode.Personal);
}
