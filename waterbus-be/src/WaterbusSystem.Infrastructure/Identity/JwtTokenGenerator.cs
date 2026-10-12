using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Identity;

/// <summary>
/// Dịch vụ cấp phát JWT Token theo chuẩn HMAC-SHA256
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Guid userId, string email, string fullName, IList<string> roles)
    {
        // Đọc đúng key cấu hình JwtSettings:Secret như Infrastructure/DependencyInjection.cs dùng để validate token.
        // Không hard-code fallback khác ở đây để tránh 2 nơi lệch secret khiến token sinh ra luôn bị 401.
        var secret = _configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException(
                "Thiếu cấu hình JwtSettings:Secret. Hãy cấu hình qua User Secrets (local) hoặc biến môi trường (deploy).");
        var issuer = _configuration["JwtSettings:Issuer"] ?? "WaterbusSystemApi";
        var audience = _configuration["JwtSettings:Audience"] ?? "WaterbusClients";
        var expiryMinutes = double.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "1440");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, fullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
