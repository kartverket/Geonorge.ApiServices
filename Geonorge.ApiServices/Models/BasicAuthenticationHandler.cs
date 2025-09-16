using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration configuration)
        : base(options, logger, encoder, clock)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization Header"));

        var authHeader = Request.Headers["Authorization"].ToString();
        if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));

        var token = authHeader.Substring("Basic ".Length).Trim();
        var credentialBytes = Convert.FromBase64String(token);
        var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':');
        if (credentials.Length != 2)
            return Task.FromResult(AuthenticateResult.Fail("Invalid Basic Authentication Credentials"));

        var username = credentials[0];
        var password = credentials[1];

        var users = _configuration.GetSection("BasicAuth:Users").Get<List<BasicAuthUser>>();
        var user = users.FirstOrDefault(u => u.Username == username && u.Password == password);

        if (user == null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid Username or Password"));

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
