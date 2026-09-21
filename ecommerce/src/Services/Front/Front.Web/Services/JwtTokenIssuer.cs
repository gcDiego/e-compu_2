using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Front.Web.Models;
using Microsoft.IdentityModel.Tokens;

namespace Front.Web.Services;

public sealed class JwtTokenIssuer(string issuer, string audience, string signingKey, TimeSpan lifetime)
{
    public IssuedToken Issue(CustomerDto customer)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, customer.Email),
            new Claim("role", "Customer"),
            new Claim("must_reset_password", customer.MustResetPassword.ToString().ToLowerInvariant())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expiresAt.UtcDateTime, signingCredentials: credentials);
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);
