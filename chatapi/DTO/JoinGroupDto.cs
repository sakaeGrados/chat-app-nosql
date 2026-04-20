using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class JoinGroupDto
    {
        [Required]
        public string GroupId { get; set; } = null!;
    }
}