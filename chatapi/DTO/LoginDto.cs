using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class LoginDto
    {
        [Required]
        public string Login { get; set; } = null!; // username or phoneNumber

        [Required]
        public string Password { get; set; } = null!;
    }
}