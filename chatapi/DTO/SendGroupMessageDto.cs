using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class SendGroupMessageDto
    {
        [Required]
        public string GroupId { get; set; } = null!;

        [Required]
        [MaxLength(500)]
        public string Content { get; set; } = null!;
    }
}