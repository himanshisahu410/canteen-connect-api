using CanteenConnect.API.Data;
using CanteenConnect.API.Hubs;
using CanteenConnect.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CanteenConnect.API.Controllers;

public class CreateOrderDto
{
    public int SlotId { get; set; }
    public string PaymentMethod { get; set; } = "wallet";
    public string? Notes { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public string? SpecialNote { get; set; }
}

public class UpdateStatusDto
{
    public string Status { get; set; } = string.Empty;
}

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public OrderController(AppDbContext db, IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/orders/mine
    // ──────────────────────────────────────────────────────────
    [HttpGet("mine")]
    public async Task<IActionResult> MyOrders()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var orders = await _db.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders);
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/orders/{id}
    // ──────────────────────────────────────────────────────────
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");

        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Slot)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound(new { message = "Order not found." });
        if (!isStaff && order.UserId != userId) return Forbid();

        return Ok(order);
    }

    // ──────────────────────────────────────────────────────────
    // POST /api/orders
    // ──────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var slot = await _db.Slots.FindAsync(dto.SlotId);
        if (slot == null) return BadRequest(new { message = "Slot not found." });
        if (slot.CurrentCount >= slot.MaxCapacity)
            return BadRequest(new { message = "Slot is full. Please choose another slot." });

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return BadRequest(new { message = "Profile not found." });

        var orderItems = new List<OrderItem>();
        decimal total = 0;

        foreach (var itemDto in dto.Items)
        {
            var menuItem = await _db.MenuItems.FindAsync(itemDto.MenuItemId);
            if (menuItem == null || !menuItem.IsAvailable)
                return BadRequest(new { message = $"{menuItem?.Name ?? "Item"} is not available." });

            total += menuItem.Price * itemDto.Quantity;
            orderItems.Add(new OrderItem
            {
                MenuItemId = itemDto.MenuItemId,
                Name = menuItem.Name,
                UnitPrice = menuItem.Price,
                Quantity = itemDto.Quantity,
                SpecialNote = itemDto.SpecialNote,
            });
        }

        if (dto.PaymentMethod == "wallet" && profile.WalletBalance < total)
            return BadRequest(new { message = $"Insufficient wallet balance. Required: ₹{total}, Available: ₹{profile.WalletBalance}" });

        var order = new Order
        {
            UserId = userId,
            SlotId = slot.Id,
            SlotDate = slot.SlotDate,
            SlotTime = slot.SlotTime,
            TotalAmount = total,
            Status = "received",
            PaymentStatus = "paid",
            PaymentMethod = dto.PaymentMethod,
            QrToken = Guid.NewGuid().ToString("N"),
            Notes = dto.Notes,
            Items = orderItems,
        };

        if (dto.PaymentMethod == "wallet")
        {
            profile.WalletBalance -= total;
            _db.WalletTransactions.Add(new WalletTransaction
            {
                UserId = userId,
                Amount = -total,
                Type = "order_payment",
                Description = $"Order payment",
            });
        }

        slot.CurrentCount++;
        profile.LoyaltyPoints += (int)(total / 10);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // ── SignalR: Staff ko new order notify karo ──────────
        await _hub.Clients.Group("staff").SendAsync("NewOrder", new
        {
            orderId = order.Id,
            totalAmount = order.TotalAmount,
            slotTime = order.SlotTime.ToString(),
            itemCount = order.Items.Count,
            message = $"New order #{order.Id} received! 🍛",
        });

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    // ──────────────────────────────────────────────────────────
    // PATCH /api/orders/{id}/status — Staff/Admin only
    // ──────────────────────────────────────────────────────────
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound(new { message = "Order not found." });

        var validStatuses = new[] { "received", "preparing", "ready", "completed", "cancelled" };
        if (!validStatuses.Contains(dto.Status))
            return BadRequest(new { message = "Invalid status." });

        order.Status = dto.Status;
        await _db.SaveChangesAsync();

        // ── SignalR: Student ko status update notify karo ────
        await _hub.Clients.Group($"user_{order.UserId}").SendAsync("OrderStatusUpdated", new
        {
            orderId = order.Id,
            status = order.Status,
            message = dto.Status switch
            {
                "preparing" => $"Order #{order.Id} is being prepared! 👨‍🍳",
                "ready" => $"Order #{order.Id} is ready for pickup! ✅",
                "completed" => $"Order #{order.Id} completed. Enjoy! 🎉",
                "cancelled" => $"Order #{order.Id} was cancelled.",
                _ => $"Order #{order.Id} status updated.",
            }
        });

        // ── SignalR: Staff ko bhi update bhejo ───────────────
        await _hub.Clients.Group("staff").SendAsync("OrderUpdated", new
        {
            orderId = order.Id,
            status = order.Status,
        });

        return Ok(new { id = order.Id, status = order.Status });
    }

    // ──────────────────────────────────────────────────────────
    // GET /api/orders/all — Staff/Admin
    // ──────────────────────────────────────────────────────────
    [HttpGet("all")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var query = _db.Orders.Include(o => o.Items).AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Status == status);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders);
    }
}