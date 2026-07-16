using CanteenConnect.API.Data;
using CanteenConnect.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CanteenConnect.API.Controllers;

public class SubmitRatingDto
{
    public int OrderId { get; set; }
    public int Stars { get; set; } // 1-5
    public string? Review { get; set; }
}

[ApiController]
[Route("api/ratings")]
[Authorize]
public class RatingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public RatingsController(AppDbContext db) => _db = db;

    // ──────────────────────────────────────────────────────────
    // POST /api/ratings — rating submit karo
    // ──────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitRatingDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (dto.Stars < 1 || dto.Stars > 5)
            return BadRequest(new { message = "Rating must be between 1 and 5." });

        // Order exist karta hai aur is student ka hai?
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.UserId == userId);
        if (order == null)
            return NotFound(new { message = "Order not found." });

        if (order.Status != "completed")
            return BadRequest(new { message = "You can only rate completed orders." });

        // Already rated?
        var existing = await _db.Ratings.FirstOrDefaultAsync(r => r.OrderId == dto.OrderId);
        if (existing != null)
            return BadRequest(new { message = "You have already rated this order." });

        var rating = new Rating
        {
            OrderId = dto.OrderId,
            UserId = userId,
            Stars = dto.Stars,
            Review = dto.Review,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Ratings.Add(rating);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Rating submitted! ⭐", rating });
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/ratings/my — apni saari ratings
    // ──────────────────────────────────────────────────────────
    [HttpGet("my")]
    public async Task<IActionResult> MyRatings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var ratings = await _db.Ratings
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new {
                r.Id,
                r.OrderId,
                r.UserId,
                stars = r.Stars,
                r.Review,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(ratings);
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/ratings/summary — Admin: overall ratings
    // ──────────────────────────────────────────────────────────
    [HttpGet("summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Summary()
    {
        var total = await _db.Ratings.CountAsync();
        var average = total > 0 ? await _db.Ratings.AverageAsync(r => (double)r.Stars) : 0;

        var distribution = await _db.Ratings
            .GroupBy(r => r.Stars)
            .Select(g => new { stars = g.Key, count = g.Count() })
            .OrderByDescending(x => x.stars)
            .ToListAsync();

        return Ok(new { total, average = Math.Round(average, 1), distribution });
    }
}