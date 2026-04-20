using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using chatapi.Services;
using chatapi.DTO;
using System.Security.Claims;

namespace chatapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? null;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        // Validate ModelState
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            var errorMessages = string.Join("; ", errors.Select(e => e.ErrorMessage));
            return BadRequest(new { message = "Validation failed", errors = errorMessages });
        }

        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.PhoneNumber))
            return BadRequest("Username, phone number and password are required");

        var success = await _authService.RegisterAsync(dto);
        if (!success)
            return BadRequest("Registration failed. Username or phone number already exists.");

        return Ok(new { message = "Registered successfully" });
    }

    /// <summary>
    /// Login and get JWT token
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Login) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Login and password are required");

        var response = await _authService.LoginAsync(dto);
        if (!response.Success)
            return Unauthorized(response.Message);

        return Ok(response);
    }

    /// <summary>
    /// Get current user profile (requires authentication)
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User not authenticated");

        // Return basic profile info
        return Ok(new { UserId = userId, Message = "Authenticated successfully" });
    }

    /// <summary>
    /// Logout endpoint (token validation only)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // JWT tokens are stateless, logout is handled client-side by removing token
        // Server can implement token blacklisting if needed
        return Ok("Logout successful");
    }
}