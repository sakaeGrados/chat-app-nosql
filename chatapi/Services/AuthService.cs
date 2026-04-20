using chatapi.Config;
using chatapi.DTO;
using chatapi.Models;
using MongoDB.Driver;
using StackExchange.Redis;
using System.Text.Json;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace chatapi.Services;

public class AuthService
{
    private readonly MongoContext _context;
    private readonly IDatabase _redis;
    private readonly JwtSettings _jwtSettings;

    public AuthService(MongoContext context, IConnectionMultiplexer redis, IOptions<JwtSettings> jwtSettings)
    {
        _context = context;
        _redis = redis.GetDatabase();
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<bool> RegisterAsync(RegisterDto dto)
    {
        var existingUser = await _context.Users.Find(u => u.Username == dto.Username || u.PhoneNumber == dto.PhoneNumber).FirstOrDefaultAsync();
        if (existingUser != null) return false;

        if (!IsValidPassword(dto.Password)) return false;

        var user = new User
        {
            Username = dto.Username,
            PhoneNumber = dto.PhoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        await _context.Users.InsertOneAsync(user);
        return true;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _context.Users.Find(u => u.Username == dto.Login || u.PhoneNumber == dto.Login).FirstOrDefaultAsync();
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return new LoginResponseDto { Success = false, Message = "Invalid credentials" };

        var token = GenerateToken(user.Id);
        // Cache user in Redis
        await _redis.StringSetAsync($"user:{user.Id}", JsonSerializer.Serialize(user), TimeSpan.FromHours(1));

        return new LoginResponseDto { Success = true, UserId = user.Id, Token = token };
    }

    private bool IsValidPassword(string password)
    {
        return password.Length >= 6 && password.Any(char.IsUpper) && password.Any(char.IsDigit);
    }

    private string GenerateToken(string userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId),
            new Claim("sub", userId)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}