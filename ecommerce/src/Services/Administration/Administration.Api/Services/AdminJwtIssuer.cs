using Administration.Api.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Administration.Api.Services;

public sealed class AdminJwtIssuer(string issuer, string audience, string signingKey, TimeSpan lifetime)
{
    public AdminLoginResponse Issue(AdminProfile admin)
    {
        var expires = DateTimeOffset.UtcNow.Add(lifetime);
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()), new Claim(JwtRegisteredClaimNames.Email, admin.Email), new Claim("role", "admin"), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expires.UtcDateTime, signingCredentials: credentials);
        return new AdminLoginResponse(admin.Id, admin.FirstName, admin.LastName, admin.Email, new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
