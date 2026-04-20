using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class SendPrivateMessageDto
    {
        [Required]
        public string ReceiverId { get; set; } = null!;

        [Required]
        [MaxLength(500)]
        public string Content { get; set; } = null!;
    }
}