using TheLedger.Domain.Identity;
using TheLedger.Domain.Tenants;
using Xunit;

namespace TheLedger.Domain.Tests;

public class FoundationsDomainTests
{
    [Fact]
    public void New_user_defaults_to_owner_role()
    {
        var user = new User { Email = "a@example.com", ExternalAuthId = "ext-1" };
        Assert.Equal(UserRole.Owner, user.Role);
    }

    [Fact]
    public void New_tenant_defaults_to_free_plan()
    {
        var tenant = new Tenant { Name = "The Fernandez Household" };
        Assert.Equal("free", tenant.Plan);
    }
}
