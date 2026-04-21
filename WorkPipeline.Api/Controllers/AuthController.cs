using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WorkPipeline.Core.DTOs;

namespace WorkPipeline.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IConfiguration config) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var expectedUser = config["Auth:Username"];
        var expectedPass = config["Auth:Password"];

        if (req.Username != expectedUser || req.Password != expectedPass)
            return Unauthorized(new { message = "Invalid credentials." });

        var token = GenerateJwt(req.Username, config["Auth:JwtSecret"]!);
        return Ok(new LoginResponse(token, req.Username));
    }

    private static string GenerateJwt(string username, string secret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(ClaimTypes.Name, username) };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
