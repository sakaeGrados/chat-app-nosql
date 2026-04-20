using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class CreateGroupDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
    }
}