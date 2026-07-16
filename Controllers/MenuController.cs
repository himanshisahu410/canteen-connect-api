using CanteenConnect.API.Data;
using CanteenConnect.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenConnect.API.Controllers;

[ApiController]
[Route("api/menu")]
[Authorize]
public class MenuController : ControllerBase
{
    private readonly AppDbContext _db;
    public MenuController(AppDbContext db) => _db = db;

    // GET /api/menu?category=Lunch&veg=true&search=dosa
    [HttpGet]
    public async Task<IActionResult> GetAll(
      [FromQuery] string? category,
      [FromQuery] bool? veg,
      [FromQuery] string? search)
    {
        var query = _db.MenuItems.AsQueryable();

        if (!string.IsNullOrEmpty(category) && category != "All")
        {
            if (category == "Popular")
                query = query.Where(m => m.IsPopular);
            else if (category == "Special")
                query = query.Where(m => m.IsSpecial);
            else
                query = query.Where(m => m.Category == category);
        }

        if (veg == true)
            query = query.Where(m => m.IsVeg);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(m => m.Name.Contains(search) ||
                                     (m.Description != null && m.Description.Contains(search)));

        var items = await query
            .OrderByDescending(m => m.IsPopular)
            .ThenBy(m => m.Name)
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/menu/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item == null) return NotFound(new { message = "Item not found." });
        return Ok(item);
    }

    // POST /api/menu — Admin only
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] MenuItem item)
    {
        item.CreatedAt = DateTime.UtcNow;
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    // PUT /api/menu/{id} — Admin only
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] MenuItem updated)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item == null) return NotFound(new { message = "Item not found." });

        item.Name = updated.Name;
        item.Description = updated.Description;
        item.Category = updated.Category;
        item.Price = updated.Price;
        item.ImageUrl = updated.ImageUrl;
        item.IsVeg = updated.IsVeg;
        item.IsAvailable = updated.IsAvailable;
        item.PrepTimeMin = updated.PrepTimeMin;
        item.Rating = updated.Rating;
        item.IsPopular = updated.IsPopular;
        item.IsSpecial = updated.IsSpecial;

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    // PATCH /api/menu/{id}/toggle — Admin only
    [HttpPatch("{id}/toggle")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleAvailability(int id)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item == null) return NotFound(new { message = "Item not found." });

        item.IsAvailable = !item.IsAvailable;
        await _db.SaveChangesAsync();
        return Ok(new { id = item.Id, isAvailable = item.IsAvailable });
    }

    // DELETE /api/menu/{id} — Admin only
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item == null) return NotFound(new { message = "Item not found." });

        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Item deleted." });
    }
}