using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class UpdateUsernameDto
    {
        [Required]
        [MinLength(2)]
        [MaxLength(50)]
        public string NewUsername { get; set; } = null!;
    }
}
