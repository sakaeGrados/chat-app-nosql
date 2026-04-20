namespace chatapi.DTO
{
    public class GroupWithLastMessageDto
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? LastMessage { get; set; }
        public string? LastMessageUsername { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int MemberCount { get; set; }
    }
}
