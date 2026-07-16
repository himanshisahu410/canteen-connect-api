using CanteenConnect.API.Data;
using CanteenConnect.API.DTOs;
using CanteenConnect.API.Models;
using CanteenConnect.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CanteenConnect.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db,
        TokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
        _tokenService = tokenService;
    }

    // ──────────────────────────────────────────────────────────
    // POST /api/auth/register
    // ──────────────────────────────────────────────────────────
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        // Email already exist karta hai?
        if (await _userManager.FindByEmailAsync(dto.Email) != null)
            return BadRequest(new { message = "Email already registered." });

        // Sirf Student aur Staff allow — Admin manually assign hoga
        var allowedRoles = new[] { "Student", "Staff" };
        var role = allowedRoles.Contains(dto.Role) ? dto.Role : "Student";

        // User banana
        var user = new AppUser
        {
            UserName = dto.Email,
            Email = dto.Email,
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // Role assign karo
        await _userManager.AddToRoleAsync(user, role);

        // Profile banana — wallet 0 se start
        var profile = new Profile
        {
            UserId = user.Id,
            FullName = dto.FullName,
            WalletBalance = 0,
            LoyaltyPoints = 0,
        };
        _db.Profiles.Add(profile);
        await _db.SaveChangesAsync();

        // Token generate karo
        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateToken(user, roles);

        return Ok(new AuthResponseDto
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email!,
            FullName = dto.FullName,
            Roles = roles.ToList()
        });
    }

    // ──────────────────────────────────────────────────────────
    // POST /api/auth/login
    // ──────────────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        // Profile se FullName lo
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateToken(user, roles);

        return Ok(new AuthResponseDto
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email!,
            FullName = profile?.FullName ?? user.Email!,
            Roles = roles.ToList()
        });
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/auth/me   [Authorize]
    // ──────────────────────────────────────────────────────────
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        var roles = await _userManager.GetRolesAsync(user!);

        return Ok(new MeResponseDto
        {
            UserId = userId,
            Email = user!.Email!,
            FullName = profile?.FullName ?? user.Email!,
            WalletBalance = profile?.WalletBalance ?? 0,
            LoyaltyPoints = profile?.LoyaltyPoints ?? 0,
            Roles = roles.ToList()
        });
    }
}