using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HealthScore.Api;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public string Mode { get; init; } = "local";
    public string? Authority { get; init; }
    public string? Audience { get; init; }
    public string? ClientId { get; init; }
    public string Scope { get; init; } = "openid profile email";
    public string LocalUser { get; init; } = "local-admin";
    public string RolesClaim { get; init; } = "roles";
    public string GroupsClaim { get; init; } = "groups";
    public Dictionary<string, string[]> RoleMappings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class SecurityExtensions
{
    public static IServiceCollection AddHealthScoreSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        ValidateOptions(options, configuration["ASPNETCORE_ENVIRONMENT"]);
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        if (string.Equals(options.Mode, "oidc", StringComparison.OrdinalIgnoreCase))
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
            {
                jwt.Authority = options.Authority;
                jwt.Audience = options.Audience;
                jwt.RequireHttpsMetadata = true;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                    NameClaimType = "name", RoleClaimType = options.RolesClaim
                };
            });
            services.AddSingleton<IClaimsTransformation, GroupRoleClaimsTransformation>();
        }
        else
        {
            services.AddAuthentication(LocalAuthenticationHandler.LocalScheme)
                .AddScheme<AuthenticationSchemeOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.LocalScheme, _ => { });
        }

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy("Viewer", policy => policy.RequireRole("Viewer", "Operator", "ScoreAdmin", "SystemAdmin"))
            .AddPolicy("Operator", policy => policy.RequireRole("Operator", "ScoreAdmin", "SystemAdmin"))
            .AddPolicy("ScoreAdmin", policy => policy.RequireRole("ScoreAdmin", "SystemAdmin"))
            .AddPolicy("SystemAdmin", policy => policy.RequireRole("SystemAdmin"));
        return services;
    }

    public static void ValidateOptions(AuthOptions options, string? environment)
    {
        if (!string.Equals(options.Mode, "local", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.Mode, "oidc", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Auth Mode must be 'local' or 'oidc'.");
        if (string.Equals(options.Mode, "local", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Local authentication is disabled in Production.");
        if (string.Equals(options.Mode, "oidc", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(options.Authority) || string.IsNullOrWhiteSpace(options.Audience) || string.IsNullOrWhiteSpace(options.ClientId)))
            throw new InvalidOperationException("Auth Authority, Audience and ClientId are required in oidc mode.");
    }
}

public sealed class GroupRoleClaimsTransformation(IOptions<AuthOptions> authOptions) : IClaimsTransformation
{
    private static readonly HashSet<string> ValidRoles = ["Viewer", "Operator", "ScoreAdmin", "SystemAdmin"];

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated) return Task.FromResult(principal);
        var options = authOptions.Value;
        var groups = principal.FindAll(options.GroupsClaim).SelectMany(claim => claim.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in options.RoleMappings.Where(mapping => ValidRoles.Contains(mapping.Key) && mapping.Value.Any(groups.Contains)))
            if (!principal.IsInRole(mapping.Key)) identity.AddClaim(new Claim(options.RolesClaim, mapping.Key));
        return Task.FromResult(principal);
    }
}

public sealed class LocalAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemes,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AuthOptions> authOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(schemes, logger, encoder)
{
    public const string LocalScheme = "Local";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authOptions.Value.LocalUser),
            new(ClaimTypes.Name, authOptions.Value.LocalUser),
            new(ClaimTypes.Role, "Viewer"), new(ClaimTypes.Role, "Operator"),
            new(ClaimTypes.Role, "ScoreAdmin"), new(ClaimTypes.Role, "SystemAdmin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, LocalScheme));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, LocalScheme)));
    }
}
