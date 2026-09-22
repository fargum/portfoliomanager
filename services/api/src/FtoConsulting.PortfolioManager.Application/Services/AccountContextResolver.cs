using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Application.Services;

public class AccountContextResolver(ICurrentUserService currentUserService, IAccountRepository accounts)
    : IAccountContextResolver
{
    public async Task<AccountContext> ResolveAsync(AccountMode mode = AccountMode.Personal)
    {
        if (mode is not (AccountMode.Personal or AccountMode.Demo))
            throw new ArgumentOutOfRangeException(nameof(mode), "Unknown account mode.");

        var personalId = await currentUserService.GetCurrentUserAccountIdAsync();
        if (mode == AccountMode.Personal)
            return new AccountContext(personalId, personalId, mode);

        var owner = await accounts.GetByIdAsync(personalId);
        if (owner == null || owner.Id != personalId || owner.Mode != AccountMode.Personal || !owner.IsActive)
            throw new UnauthorizedAccessException("Demo account is unavailable.");

        var demo = await accounts.GetDemoByOwnerAccountIdAsync(personalId);
        if (demo == null || !owner.CanAccessDemo(demo))
            throw new UnauthorizedAccessException("Demo account is unavailable.");

        return new AccountContext(personalId, demo.Id, mode);
    }
}
