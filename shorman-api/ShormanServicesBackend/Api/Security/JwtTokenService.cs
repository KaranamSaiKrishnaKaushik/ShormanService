using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Security;

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Customer = "Customer";
    public const string Rider = "Rider";

    public static readonly string[] All = [SuperAdmin, Admin, Customer, Rider];
    public static readonly string[] CustomerFacing = [SuperAdmin, Admin, Customer];
    public static readonly string[] RiderFacing = [SuperAdmin, Rider];

    public static bool IsValid(string role) => All.Contains(role, StringComparer.OrdinalIgnoreCase);
}

public class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions jwtOptions = options.Value;

    public string CreateToken(ApiUser user)
    {
        var roles = user.UserRoles
            .Select(x => x.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
