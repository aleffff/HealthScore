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
    public string LocalUser { get; init; } = "local-admin";
}

public static class SecurityExtensions
{
    public static IServiceCollection AddHealthScoreSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        if (string.Equals(options.Mode, "oidc", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.Authority) || string.IsNullOrWhiteSpace(options.Audience))
                throw new InvalidOperationException("Auth Authority and Audience are required in oidc mode.");
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
            {
                jwt.Authority = options.Authority;
                jwt.Audience = options.Audience;
                jwt.RequireHttpsMetadata = true;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                    NameClaimType = "name", RoleClaimType = "roles"
                };
            });
        }
        else
        {
            services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
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
