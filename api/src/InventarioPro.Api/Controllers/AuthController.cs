using System.Security.Claims;
using InventarioPro.Api.Common;
using InventarioPro.Api.Domain.Entities;
using InventarioPro.Api.Dtos;
using InventarioPro.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventarioPro.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
// Rate limiting estricto en autenticación: es el endpoint que se ataca
// por fuerza bruta y credential stuffing.
[EnableRateLimiting("auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ITokenService tokens,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>Registra un usuario nuevo. Se le asigna el rol Viewer por defecto.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = dto.FullName.Trim()
        };

        var result = await userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            throw new DomainException(errors);
        }

        // Rol mínimo por defecto. Elevar a Manager o Admin es una acción explícita.
        await userManager.AddToRoleAsync(user, Roles.Viewer);

        logger.LogInformation("Usuario registrado: {Email}", email);

        var (access, refresh, expiresAt) = await tokens.IssueTokensAsync(user, ct);
        var roles = await userManager.GetRolesAsync(user);

        return Ok(new AuthResponseDto(access, refresh, expiresAt, user.Email!, user.FullName, roles));
    }

    /// <summary>Inicia sesión y devuelve access token + refresh token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        // Mensaje idéntico si el usuario no existe o si la contraseña es incorrecta:
        // distinguirlos permitiría enumerar qué correos están registrados.
        if (user is null)
        {
            logger.LogWarning("Login fallido para {Email}: usuario inexistente", email);
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login bloqueado para {Email}: cuenta con lockout activo", email);
            return Unauthorized(new { message = "Cuenta bloqueada temporalmente por intentos fallidos." });
        }

        if (!await userManager.CheckPasswordAsync(user, dto.Password))
        {
            // Incrementa el contador de fallos: dispara el lockout de Identity.
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Login fallido para {Email}: contraseña incorrecta", email);
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var (access, refresh, expiresAt) = await tokens.IssueTokensAsync(user, ct);
        var roles = await userManager.GetRolesAsync(user);

        logger.LogInformation("Login exitoso: {Email}", email);

        return Ok(new AuthResponseDto(access, refresh, expiresAt, user.Email!, user.FullName, roles));
    }

    /// <summary>Canjea un refresh token por un par nuevo. El token anterior queda revocado.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Refresh(
        [FromBody] RefreshDto dto, CancellationToken ct)
    {
        try
        {
            var (access, refresh, expiresAt, user) = await tokens.RotateAsync(dto.RefreshToken, ct);
            var roles = await userManager.GetRolesAsync(user);

            return Ok(new AuthResponseDto(access, refresh, expiresAt, user.Email!, user.FullName, roles));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>Cierra todas las sesiones activas del usuario autenticado.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
            await tokens.RevokeAllAsync(userId, ct);

        return NoContent();
    }

    /// <summary>Devuelve el perfil del usuario autenticado.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            Roles = roles
        });
    }
}
