using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class UpdateUserStatusDto
    {
        [Required]
        [RegularExpression("^(online|away|dnd|invisible)$")]
        public string Status { get; set; } = "online";
    }
}
