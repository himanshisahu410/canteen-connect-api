using Microsoft.AspNetCore.Identity;

namespace CanteenConnect.API.Models;

// ==========================================
// AppUser — Identity ka base extend karte hain
// AspNetUsers table mein jaayega automatically
// ==========================================
public class AppUser : IdentityUser
{
    // IdentityUser mein already hai:
    // Id, Email, UserName, PasswordHash, PhoneNumber etc.
    // Extra fields Profile model mein hain
}

// ==========================================
// Profile — Wallet balance, loyalty points
// ==========================================
public class Profile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public decimal WalletBalance { get; set; } = 0;
    public int LoyaltyPoints { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ==========================================
// MenuItem — Menu ka ek item
// ==========================================
public class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsVeg { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public int PrepTimeMin { get; set; } = 10;
    public decimal Rating { get; set; } = 4.3m;
    public bool IsPopular { get; set; } = false;
    public bool IsSpecial { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ==========================================
// Slot — Pickup time slot
// ==========================================
public class Slot
{
    public int Id { get; set; }
    public DateOnly SlotDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public int MaxCapacity { get; set; } = 8;
    public int CurrentCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ==========================================
// Order — Ek complete order
// ==========================================
public class Order
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? SlotId { get; set; }
    public DateOnly SlotDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "received";
    public string PaymentStatus { get; set; } = "paid";
    public string PaymentMethod { get; set; } = "wallet";
    public string QrToken { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Slot? Slot { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

// ==========================================
// OrderItem — Order ke andar ek item
// ==========================================
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public string? SpecialNote { get; set; }

    // Navigation properties
    public Order? Order { get; set; }
    public MenuItem? MenuItem { get; set; }
}

// ==========================================
// WalletTransaction — Wallet ki history
// ==========================================
public class WalletTransaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }  // positive = credit, negative = debit
    public string Type { get; set; } = string.Empty; // topup|order_payment|refund
    public string? Description { get; set; }
    public int? OrderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Rating model
public class Rating
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string UserId { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Schema.Column("Rating")]
    public int Stars { get; set; }

    public string? Review { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Order? Order { get; set; }
}
