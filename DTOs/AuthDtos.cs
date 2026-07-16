namespace CanteenConnect.API.DTOs;

// ── Register ─────────────────────────────────────────────────
public class RegisterDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Student"; // Student | Staff
}

// ── Login ────────────────────────────────────────────────────
public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// ── Auth Response — token + user info ────────────────────────
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

// ── Me Response — apni profile ───────────────────────────────
public class MeResponseDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public decimal WalletBalance { get; set; }
    public int LoyaltyPoints { get; set; }
    public List<string> Roles { get; set; } = new();
}