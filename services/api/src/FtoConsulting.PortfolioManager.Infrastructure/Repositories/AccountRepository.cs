using Microsoft.EntityFrameworkCore;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories;

public class AccountRepository : Repository<Account>, IAccountRepository
{
    public AccountRepository(PortfolioManagerDbContext context) : base(context)
    {
    }

    public async Task<Account?> GetByExternalUserIdAsync(string externalUserId)
    {
        return await _dbSet
            .Include(a => a.Portfolios)
            .FirstOrDefaultAsync(a => a.ExternalUserId == externalUserId);
    }

    public async Task<Account?> GetByEmailAsync(string email)
    {
        return await _dbSet
            .Include(a => a.Portfolios)
            .FirstOrDefaultAsync(a => a.Email == email);
    }

    public async Task<bool> ExternalUserIdExistsAsync(string externalUserId)
    {
        return await _dbSet.AnyAsync(a => a.ExternalUserId == externalUserId);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _dbSet.AnyAsync(a => a.Email == email);
    }

    public async Task<Account> CreateOrUpdateExternalUserAsync(string externalUserId, string email, string displayName)
    {
        // Try to find existing account first
        var existingAccount = await GetByExternalUserIdAsync(externalUserId) 
                           ?? await GetByEmailAsync(email);

        if (existingAccount != null)
        {
            // Update existing account
            existingAccount.UpdateUserInfo(email, displayName);
            existingAccount.RecordLogin();
            await UpdateAsync(existingAccount);
            return existingAccount;
        }

        // Create new account
        var newAccount = new Account(externalUserId, email, displayName);
        newAccount.RecordLogin();
        await AddAsync(newAccount);
        return newAccount;
    }

    public async Task<IEnumerable<Account>> GetActiveAccountsAsync()
    {
        return await _dbSet
            .Include(a => a.Portfolios)
            .Where(a => a.IsActive)
            .OrderBy(a => a.Email)
            .ToListAsync();
    }
}