using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using InventarioPro.Api.Data;
using InventarioPro.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace InventarioPro.Api.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Clave de firma. Se carga de variable de entorno o user-secrets, nunca del appsettings versionado.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Vida del access token. Corta a propósito: si se filtra, expira rápido.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Vida del refresh token.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}

public interface ITokenService
{
    Task<(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)>
        IssueTokensAsync(ApplicationUser user, CancellationToken ct = default);

    Task<(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, ApplicationUser User)>
        RotateAsync(string refreshToken, CancellationToken ct = default);

    Task RevokeAllAsync(string userId, CancellationToken ct = default);
}

public class TokenService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    Microsoft.Extensions.Options.IOptions<JwtOptions> options,
    ILogger<TokenService> logger) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

    public async Task<(string, string, DateTimeOffset)> IssueTokensAsync(
        ApplicationUser user, CancellationToken ct = default)
    {
        var (access, expiresAt) = await CreateAccessTokenAsync(user);
        var refresh = await CreateRefreshTokenAsync(user.Id, ct);
        return (access, refresh, expiresAt);
    }

    public async Task<(string, string, DateTimeOffset, ApplicationUser)> RotateAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);

        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            throw new UnauthorizedAccessException("Refresh token inválido.");

        // Detección de reuso: si llega un token ya revocado, alguien está usando
        // una copia robada. Se revoca toda la familia de tokens del usuario.
        if (stored.RevokedAt is not null)
        {
            logger.LogWarning(
                "Reuso de refresh token detectado para el usuario {UserId}. Revocando todas las sesiones.",
                stored.UserId);

            await RevokeAllAsync(stored.UserId, ct);
            throw new UnauthorizedAccessException("Refresh token inválido.");
        }

        if (!stored.IsActive)
            throw new UnauthorizedAccessException("Refresh token expirado.");

        var user = stored.User ?? throw new UnauthorizedAccessException("Usuario no encontrado.");

        var (access, expiresAt) = await CreateAccessTokenAsync(user);
        var newRefresh = await CreateRefreshTokenAsync(user.Id, ct, persist: false);

        stored.RevokedAt = DateTimeOffset.UtcNow;
        stored.ReplacedByTokenHash = Hash(newRefresh);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(newRefresh),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays)
        });

        await db.SaveChangesAsync(ct);

        return (access, newRefresh, expiresAt, user);
    }

    public async Task RevokeAllAsync(string userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private async Task<(string Token, DateTimeOffset ExpiresAt)> CreateAccessTokenAsync(ApplicationUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            // jti único: permite invalidar un access token puntual si hiciera falta.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.FullName))
            claims.Add(new Claim(ClaimTypes.Name, user.FullName));

        foreach (var role in await userManager.GetRolesAsync(user))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private async Task<string> CreateRefreshTokenAsync(
        string userId, CancellationToken ct, bool persist = true)
    {
        // 256 bits de aleatoriedad criptográfica. Nada de Guid ni Random.
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);

        if (persist)
        {
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenHash = Hash(token),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays)
            });

            await db.SaveChangesAsync(ct);
        }

        return token;
    }

    /// <summary>
    /// SHA-256 del token. En la base se guarda solo el hash: si alguien la lee,
    /// no obtiene tokens usables.
    /// </summary>
    private static string Hash(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
