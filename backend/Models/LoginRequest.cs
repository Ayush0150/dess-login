using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class LoginRequest
{
    [Required, StringLength(100)]
    public string Username { get; set; } = "";

    [Required, StringLength(1024)]
    public string Password { get; set; } = "";
}
