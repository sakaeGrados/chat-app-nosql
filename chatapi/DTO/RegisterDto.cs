using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "El usuario es requerido")]
        [MaxLength(50, ErrorMessage = "El usuario no puede exceder 50 caracteres")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "El teléfono es requerido")]
        [MaxLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres")]
        public string PhoneNumber { get; set; } = null!;

        [Required(ErrorMessage = "La contraseña es requerida")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
        public string Password { get; set; } = null!;
    }
}