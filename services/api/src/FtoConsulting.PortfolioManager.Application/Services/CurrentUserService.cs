using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Domain.Repositories;

namespace FtoConsulting.PortfolioManager.Application.Services;

public class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    ILogger<CurrentUserService> logger) : ICurrentUserService
{

    public string GetCurrentUserId()
    {
        var user = httpContextAccessor.HttpContext?.User;
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
            logger.LogWarning("Unable to determine user ID from claims. Available claims: {Claims}",
                string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}")));
            throw new InvalidOperationException("Unable to determine user ID from authentication token");
        }

        return userId;
    }

    public string GetCurrentUserEmail()
    {
        var user = httpContextAccessor.HttpContext?.User;
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
        
        logger.LogInformation("Getting account for user {UserId} ({Email})", userId, email);
        
        // Try to get or create the account using proper account management
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
            logger.LogInformation("Creating new account for external user {UserId} ({Email})", userId, email);
            account = await accountRepository.CreateOrUpdateExternalUserAsync(userId, email, displayName);
            await unitOfWork.SaveChangesAsync();
        }
        else
        {
            // Update existing account info and record login
            var displayName = GetCurrentUserDisplayName();
            account.UpdateUserInfo(email, displayName);
            account.RecordLogin();
            await accountRepository.UpdateAsync(account);
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Found existing account {AccountId} for user {Email}", account.Id, email);
        }
        
        if (!account.IsActive)
        {
            throw new UnauthorizedAccessException($"Account {account.Id} is deactivated");
        }
        
        return account.Id;
    }
    
    private string GetCurrentUserDisplayName()
    {
        var user = httpContextAccessor.HttpContext?.User;
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
}