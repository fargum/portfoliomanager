using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

public interface IAccountRepository : IRepository<Account>
{
    Task<Account?> GetByUserNameAsync(string userName);
    Task<bool> UserNameExistsAsync(string userName);
}