using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class SendMessageDto
    {
        [Required]
        [MaxLength(500)]
        public string Content { get; set; } = null!;
    }
}