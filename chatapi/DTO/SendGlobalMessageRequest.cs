namespace chatapi.DTO
{
    public class SendGlobalMessageRequest
    {
        public string UserId { get; set; } = null!;
        public string Content { get; set; } = null!;
    }
}