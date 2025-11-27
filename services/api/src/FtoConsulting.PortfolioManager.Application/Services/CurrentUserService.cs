using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Application.Services;

public class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    ILogger<CurrentUserService> logger) : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<CurrentUserService> _logger = logger;

    public string GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        // Try different claim types for user ID from Azure AD
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                    user.FindFirst("sub")?.Value ??
                    user.FindFirst("oid")?.Value ??
                    user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unable to determine user ID from claims. Available claims: {Claims}",
                string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}")));
            throw new InvalidOperationException("Unable to determine user ID from authentication token");
        }

        return userId;
    }

    public string GetCurrentUserEmail()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        var email = user.FindFirst(ClaimTypes.Email)?.Value ??
                   user.FindFirst("email")?.Value ??
                   user.FindFirst("preferred_username")?.Value ??
                   user.FindFirst("upn")?.Value;

        if (string.IsNullOrEmpty(email))
        {
            throw new InvalidOperationException("Unable to determine user email from authentication token");
        }

        return email;
    }

    public async Task<int> GetCurrentUserAccountIdAsync()
    {
        var userId = GetCurrentUserId();
        var email = GetCurrentUserEmail();
        
        _logger.LogInformation("Getting account for user {UserId} ({Email})", userId, email);
        
        // Try to get or create the account using proper account management
        var accountRepository = GetAccountRepository();
        
        // First, try to find existing account by external user ID
        var account = await accountRepository.GetByExternalUserIdAsync(userId);
        
        if (account == null)
        {
            // If not found, try by email (in case user was created differently)
            account = await accountRepository.GetByEmailAsync(email);
        }
        
        if (account == null)
        {
            // Create new account for this external user
            var displayName = GetCurrentUserDisplayName();
            _logger.LogInformation("Creating new account for external user {UserId} ({Email})", userId, email);
            account = await accountRepository.CreateOrUpdateExternalUserAsync(userId, email, displayName);
        }
        else
        {
            // Update existing account info and record login
            var displayName = GetCurrentUserDisplayName();
            account.UpdateUserInfo(email, displayName);
            account.RecordLogin();
            await accountRepository.UpdateAsync(account);
            _logger.LogInformation("Found existing account {AccountId} for user {Email}", account.Id, email);
        }
        
        if (!account.IsActive)
        {
            throw new UnauthorizedAccessException($"Account {account.Id} is deactivated");
        }
        
        return account.Id;
    }
    
    private string GetCurrentUserDisplayName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        return user.FindFirst("name")?.Value ??
               user.FindFirst(ClaimTypes.Name)?.Value ??
               user.FindFirst("given_name")?.Value ??
               user.FindFirst("preferred_username")?.Value ??
               GetCurrentUserEmail(); // Fallback to email
    }
    
    // This is a temporary hack until we implement proper dependency injection for repositories in services
    // In a proper implementation, we would inject IAccountRepository through constructor
    private IAccountRepository GetAccountRepository()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.RequestServices == null)
        {
            throw new InvalidOperationException("Cannot access HTTP context or request services");
        }
        
        var accountRepository = httpContext.RequestServices.GetService(typeof(IAccountRepository)) as IAccountRepository;
        if (accountRepository == null)
        {
            throw new InvalidOperationException("IAccountRepository is not registered in DI container");
        }
        
        return accountRepository;
    }
}