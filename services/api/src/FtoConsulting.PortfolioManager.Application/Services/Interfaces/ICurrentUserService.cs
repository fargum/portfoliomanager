namespace FtoConsulting.PortfolioManager.Application.Services.Interfaces;

public interface ICurrentUserService
{
    Task<int> GetCurrentUserAccountIdAsync();
    string GetCurrentUserId();
    string GetCurrentUserEmail();
}