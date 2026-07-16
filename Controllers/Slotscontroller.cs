using CanteenConnect.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenConnect.API.Controllers;

[ApiController]
[Route("api/slots")]
[Authorize]
public class SlotsController : ControllerBase
{
    private readonly AppDbContext _db;
    public SlotsController(AppDbContext db) => _db = db;

    // ──────────────────────────────────────────────────────────
    // GET /api/slots?date=2026-05-14 — ek din ke available slots
    // ──────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetSlots([FromQuery] string? date)
    {
        // Date parse karo — default aaj ka din
        DateOnly targetDate;
        if (!DateOnly.TryParse(date, out targetDate))
            targetDate = DateOnly.FromDateTime(DateTime.Today);

        var slots = await _db.Slots
            .Where(s => s.SlotDate == targetDate)
            .OrderBy(s => s.SlotTime)
            .Select(s => new
            {
                s.Id,
                s.SlotDate,
                s.SlotTime,
                s.MaxCapacity,
                s.CurrentCount,
                AvailableCount = s.MaxCapacity - s.CurrentCount,
                IsFull = s.CurrentCount >= s.MaxCapacity
            })
            .ToListAsync();

        return Ok(slots);
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/slots/available — aaj + kal ke available slots
    // ──────────────────────────────────────────────────────────
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);

        var slots = await _db.Slots
            .Where(s => s.SlotDate >= today && s.SlotDate <= tomorrow)
            .Where(s => s.CurrentCount < s.MaxCapacity) // sirf available
            .OrderBy(s => s.SlotDate)
            .ThenBy(s => s.SlotTime)
            .Select(s => new
            {
                s.Id,
                s.SlotDate,
                s.SlotTime,
                s.MaxCapacity,
                s.CurrentCount,
                AvailableCount = s.MaxCapacity - s.CurrentCount,
                IsFull = false
            })
            .ToListAsync();

        return Ok(slots);
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/slots/{id} — ek specific slot
    // ──────────────────────────────────────────────────────────
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var slot = await _db.Slots.FindAsync(id);
        if (slot == null) return NotFound(new { message = "Slot not found." });
        return Ok(slot);
    }

    // ──────────────────────────────────────────────────────────
    // POST /api/slots/generate — Admin: kal ke slots generate karo
    // ──────────────────────────────────────────────────────────
    [HttpPost("generate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateSlots([FromQuery] string? date)
    {
        DateOnly targetDate;
        if (!DateOnly.TryParse(date, out targetDate))
            targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        int created = 0;
        for (int i = 0; i <= 14; i++) // 11:30 to 15:00, har 15 min
        {
            var time = new TimeOnly(11, 30).AddMinutes(i * 15);
            var exists = await _db.Slots
                .AnyAsync(s => s.SlotDate == targetDate && s.SlotTime == time);

            if (!exists)
            {
                _db.Slots.Add(new CanteenConnect.API.Models.Slot
                {
                    SlotDate = targetDate,
                    SlotTime = time,
                    MaxCapacity = 8,
                    CurrentCount = 0
                });
                created++;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = $"{created} slots created for {targetDate}." });
    }
}