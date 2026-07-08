using System.Security.Claims;
using HealthScore.Api;
using Microsoft.Extensions.Options;

namespace HealthScore.UnitTests;

public sealed class SecurityTests
{
    [Fact]
    public void Local_authentication_is_rejected_in_production()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SecurityExtensions.ValidateOptions(new AuthOptions { Mode = "local" }, "Production"));
        Assert.Contains("disabled", error.Message);
    }

    [Fact]
    public void Oidc_requires_browser_and_api_configuration()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SecurityExtensions.ValidateOptions(new AuthOptions { Mode = "oidc", Authority = "https://id.example", Audience = "api" }, "Production"));
    }

    [Fact]
    public async Task Corporate_group_is_mapped_to_application_role()
    {
        var options = Options.Create(new AuthOptions
        {
            GroupsClaim = "groups",
            RolesClaim = ClaimTypes.Role,
            RoleMappings = new Dictionary<string, string[]> { ["ScoreAdmin"] = ["healthscore-admin"] }
        });
        var identity = new ClaimsIdentity([new Claim("groups", "healthscore-admin")], "oidc", ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        await new GroupRoleClaimsTransformation(options).TransformAsync(principal);

        Assert.True(principal.IsInRole("ScoreAdmin"));
    }
}
