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

    public async Task<Account?> GetByUserNameAsync(string userName)
    {
        return await _dbSet
            .Include(a => a.Portfolios)
            .FirstOrDefaultAsync(a => a.UserName == userName);
    }

    public async Task<bool> UserNameExistsAsync(string userName)
    {
        return await _dbSet.AnyAsync(a => a.UserName == userName);
    }
}