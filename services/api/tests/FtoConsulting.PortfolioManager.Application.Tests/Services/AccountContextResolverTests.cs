using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Moq;

namespace FtoConsulting.PortfolioManager.Application.Tests.Services;

public class AccountContextResolverTests
{
    private readonly Mock<ICurrentUserService> currentUser = new(MockBehavior.Strict);
    private readonly Mock<IAccountRepository> accounts = new(MockBehavior.Strict);

    private AccountContextResolver Resolver => new(currentUser.Object, accounts.Object);

    private static Account Personal(int id)
    {
        var account = new Account($"identity-{id}", $"user-{id}@example.com", "Personal");
        SetId(account, id);
        return account;
    }

    // Simulates database-generated keys without exposing mutation in the domain API.
    private static void SetId(Account account, int id) =>
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(account, id);

    [Fact]
    public async Task ResolveAsync_DefaultMode_PreservesPersonalResolution()
    {
        currentUser.Setup(x => x.GetCurrentUserAccountIdAsync()).ReturnsAsync(42);
        Assert.Equal(new AccountContext(42, 42, AccountMode.Personal), await Resolver.ResolveAsync());
        accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveAsync_OwnedDemo_ReturnsSeparateAccount()
    {
        var owner = Personal(42);
        var demo = owner.CreateDemoAccount("Interview demo");
        SetId(demo, 99);
        currentUser.Setup(x => x.GetCurrentUserAccountIdAsync()).ReturnsAsync(owner.Id);
        accounts.Setup(x => x.GetByIdAsync(owner.Id)).ReturnsAsync(owner);
        accounts.Setup(x => x.GetDemoByOwnerAccountIdAsync(owner.Id)).ReturnsAsync(demo);

        Assert.Equal(new AccountContext(42, 99, AccountMode.Demo), await Resolver.ResolveAsync(AccountMode.Demo));
        Assert.Null(demo.ExternalUserId);
        Assert.Null(demo.Email);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("other-owner")]
    [InlineData("personal")]
    public async Task ResolveAsync_UnavailableOrUnauthorizedDemo_ThrowsWithoutFallback(string scenario)
    {
        var owner = Personal(42);
        Account? demo = scenario switch
        {
            "missing" => null,
            "other-owner" => Personal(43).CreateDemoAccount("Other demo"),
            "personal" => Personal(99),
            _ => owner.CreateDemoAccount("Demo")
        };
        if (demo != null) SetId(demo, 99);
        if (scenario == "inactive") demo!.Deactivate();
        currentUser.Setup(x => x.GetCurrentUserAccountIdAsync()).ReturnsAsync(42);
        accounts.Setup(x => x.GetByIdAsync(42)).ReturnsAsync(owner);
        accounts.Setup(x => x.GetDemoByOwnerAccountIdAsync(42)).ReturnsAsync(demo);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Resolver.ResolveAsync(AccountMode.Demo));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResolveAsync_MissingOrInactiveOwner_Throws(bool missing)
    {
        var owner = Personal(42);
        owner.Deactivate();
        currentUser.Setup(x => x.GetCurrentUserAccountIdAsync()).ReturnsAsync(42);
        accounts.Setup(x => x.GetByIdAsync(42)).ReturnsAsync(missing ? null : owner);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Resolver.ResolveAsync(AccountMode.Demo));
    }

    [Fact]
    public async Task ResolveAsync_UnknownMode_RejectsBeforeResolvingIdentity()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Resolver.ResolveAsync((AccountMode)123));
        currentUser.VerifyNoOtherCalls();
        accounts.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AccountMode.Personal)]
    [InlineData(AccountMode.Demo)]
    public async Task ResolveAsync_Unauthenticated_Throws(AccountMode mode)
    {
        currentUser.Setup(x => x.GetCurrentUserAccountIdAsync()).ThrowsAsync(new UnauthorizedAccessException());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Resolver.ResolveAsync(mode));
        accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public void CreateDemoAccount_InvalidOwner_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Personal(0).CreateDemoAccount("Demo"));
        var owner = Personal(42);
        var demo = owner.CreateDemoAccount("Demo");
        SetId(demo, 99);
        Assert.Throws<InvalidOperationException>(() => demo.CreateDemoAccount("Nested"));
        owner.Deactivate();
        Assert.Throws<InvalidOperationException>(() => owner.CreateDemoAccount("Demo"));
    }

    [Fact]
    public void DemoAccount_ExternalIdentityMutation_Throws()
    {
        var demo = Personal(42).CreateDemoAccount("Demo");
        Assert.Throws<InvalidOperationException>(() => demo.UpdateUserInfo("user@example.com", "User"));
        Assert.Throws<InvalidOperationException>(() => demo.RecordLogin());
    }
}
