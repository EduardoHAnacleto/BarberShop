namespace BarberShop.Application.DTOs;

public class LoginDTO
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;

    // When true, the server issues a long-lived JWT (30 days) so the client
    // stays signed in across browser restarts without re-authenticating.
    public bool RememberMe { get; set; }
}
