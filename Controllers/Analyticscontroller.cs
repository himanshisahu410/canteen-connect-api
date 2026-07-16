using CanteenConnect.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CanteenConnect.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Admin")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AnalyticsController(AppDbContext db) => _db = db;

    // ──────────────────────────────────────────────────────────
    // GET /api/analytics/summary — overall numbers
    // ──────────────────────────────────────────────────────────
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var totalOrders = await _db.Orders.CountAsync();
        var todayOrders = await _db.Orders.CountAsync(o => o.SlotDate == today);
        var totalRevenue = await _db.Orders
            .Where(o => o.Status != "cancelled")
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
        var todayRevenue = await _db.Orders
            .Where(o => o.SlotDate == today && o.Status != "cancelled")
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
        var totalStudents = await _db.Profiles.CountAsync();
        var pendingOrders = await _db.Orders
            .CountAsync(o => o.Status == "received" || o.Status == "preparing");

        return Ok(new
        {
            totalOrders,
            todayOrders,
            totalRevenue,
            todayRevenue,
            totalStudents,
            pendingOrders,
        });
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/analytics/revenue?days=7 — revenue last N days
    // ──────────────────────────────────────────────────────────
    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] int days = 7)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-(days - 1)));

        // Pehle data fetch karo, phir client side pe process karo
        var data = await _db.Orders
            .Where(o => o.SlotDate >= from && o.Status != "cancelled")
            .Select(o => new { o.SlotDate, o.TotalAmount })
            .ToListAsync();

        var grouped = data
            .GroupBy(o => o.SlotDate)
            .Select(g => new { date = g.Key, revenue = g.Sum(o => o.TotalAmount), orders = g.Count() })
            .OrderBy(x => x.date)
            .ToList();

        var result = new List<object>();
        for (int i = 0; i < days; i++)
        {
            var d = DateOnly.FromDateTime(DateTime.Today.AddDays(-(days - 1 - i)));
            var found = grouped.FirstOrDefault(x => x.date == d);
            result.Add(new { date = d.Day + "/" + d.Month, revenue = found?.revenue ?? 0, orders = found?.orders ?? 0 });
        }
        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/analytics/popular — top 5 most ordered items
    // ──────────────────────────────────────────────────────────
    [HttpGet("popular")]
    public async Task<IActionResult> Popular()
    {
        var data = await _db.OrderItems
            .GroupBy(oi => new { oi.MenuItemId, oi.Name })
            .Select(g => new
            {
                name = g.Key.Name,
                quantity = g.Sum(oi => oi.Quantity),
                revenue = g.Sum(oi => oi.UnitPrice * oi.Quantity),
            })
            .OrderByDescending(x => x.quantity)
            .Take(6)
            .ToListAsync();

        return Ok(data);
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/analytics/orders-by-status — pie chart data
    // ──────────────────────────────────────────────────────────
    [HttpGet("orders-by-status")]
    public async Task<IActionResult> OrdersByStatus()
    {
        var data = await _db.Orders
            .GroupBy(o => o.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        return Ok(data);
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/analytics/peak-hours — orders by slot time
    // ──────────────────────────────────────────────────────────
    [HttpGet("peak-hours")]
    public async Task<IActionResult> PeakHours()
    {
        // Pehle data fetch karo
        var data = await _db.Orders
            .Select(o => new { o.SlotTime })
            .ToListAsync();

        var grouped = data
            .GroupBy(o => o.SlotTime)
            .Select(g => new { time = g.Key.ToString("HH:mm"), orders = g.Count() })
            .OrderBy(x => x.time)
            .ToList();

        return Ok(grouped);
    }
    // GET /api/analytics/ratings
    [HttpGet("ratings")]
    public async Task<IActionResult> RatingsSummary()
    {
        var total = await _db.Ratings.CountAsync();
        var average = total > 0 ? Math.Round(await _db.Ratings.AverageAsync(r => (double)r.Stars), 1) : 0.0;

        var distribution = await _db.Ratings
            .GroupBy(r => r.Stars)
            .Select(g => new { stars = g.Key, count = g.Count() })
            .OrderByDescending(x => x.stars)
            .ToListAsync();

        var recent = await _db.Ratings
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => new { r.Stars, r.Review, r.CreatedAt })
            .ToListAsync();

        return Ok(new { total, average, distribution, recent });
    }
    // GET /api/ratings/my
    [HttpGet("my")]
    public async Task<IActionResult> MyRatings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var ratings = await _db.Ratings
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(ratings);
    }
}