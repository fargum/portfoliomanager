using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

public interface IAccountRepository : IRepository<Account>
{
    // External user management methods
    Task<Account?> GetByExternalUserIdAsync(string externalUserId);
    Task<Account?> GetByEmailAsync(string email);
    Task<bool> ExternalUserIdExistsAsync(string externalUserId);
    Task<bool> EmailExistsAsync(string email);
    Task<Account> CreateOrUpdateExternalUserAsync(string externalUserId, string email, string displayName);
    Task<IEnumerable<Account>> GetActiveAccountsAsync();
}