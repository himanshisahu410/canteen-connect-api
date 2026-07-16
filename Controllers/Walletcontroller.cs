using CanteenConnect.API.Data;
using CanteenConnect.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CanteenConnect.API.Controllers;

public class TopupDto
{
    public decimal Amount { get; set; }
}

[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly AppDbContext _db;
    public WalletController(AppDbContext db) => _db = db;

    // ──────────────────────────────────────────────────────────
    // GET /api/wallet — current balance + recent transactions
    // ──────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetWallet()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return NotFound(new { message = "Profile not found." });

        var transactions = await _db.WalletTransactions
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(20)
            .ToListAsync();

        return Ok(new
        {
            balance = profile.WalletBalance,
            loyaltyPoints = profile.LoyaltyPoints,
            transactions
        });
    }

    // ──────────────────────────────────────────────────────────
    // POST /api/wallet/topup — wallet mein paisa daalo
    // ──────────────────────────────────────────────────────────
    [HttpPost("topup")]
    public async Task<IActionResult> Topup([FromBody] TopupDto dto)
    {
        if (dto.Amount <= 0 || dto.Amount > 10000)
            return BadRequest(new { message = "Amount must be between ₹1 and ₹10,000." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return NotFound(new { message = "Profile not found." });

        profile.WalletBalance += dto.Amount;

        _db.WalletTransactions.Add(new WalletTransaction
        {
            UserId = userId,
            Amount = dto.Amount,
            Type = "topup",
            Description = $"Wallet topped up with ₹{dto.Amount}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = $"₹{dto.Amount} added successfully!",
            newBalance = profile.WalletBalance
        });
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/wallet/transactions — poori history
    // ──────────────────────────────────────────────────────────
    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var transactions = await _db.WalletTransactions
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        return Ok(transactions);
    }
}