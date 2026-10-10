using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CvApi;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly CvDb _database;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<ApiUser> _passwordHasher;

    public AuthController(CvDb database, IConfiguration configuration, IPasswordHasher<ApiUser> passwordHasher)
    {
        _database = database;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.SingleOrDefaultAsync(
            candidate => candidate.Username == request.Username,
            cancellationToken);

        var userToVerify = user ?? new ApiUser();
        var passwordHash = user?.PasswordHash;
        var verificationResult = _passwordHasher.VerifyHashedPassword(
            userToVerify,
            passwordHash,
            request.Password);

        if (user is null || verificationResult == PasswordVerificationResult.Failed)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Neplatné přihlašovací údaje.");
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _database.SaveChangesAsync(cancellationToken);
        }

        var configuredMinutes = _configuration.GetValue("Jwt:ExpiresMinutes", 30);
        var expirationMinutes = Math.Clamp(configuredMinutes, 1, 120);
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var keyBytes = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var signingCredentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var accessToken = tokenHandler.WriteToken(token);

        return new TokenResponse(accessToken, "Bearer", expiresAt);
    }
}
