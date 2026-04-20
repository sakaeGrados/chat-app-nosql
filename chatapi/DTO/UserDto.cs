using System.ComponentModel.DataAnnotations;

namespace chatapi.DTO
{
    public class UserDto
    {
        public string Id { get; set; } = null!;

        [Required]
        public string Username { get; set; } = null!;

        [Required]
        public string PhoneNumber { get; set; } = null!;

        /// <summary>
        /// User status (online, away, dnd, invisible, offline)
        /// Only visible if:
        /// - User is viewing their own profile, OR
        /// - Users have accepted friend connection
        /// Otherwise returns null for privacy
        /// </summary>
        public string? Status { get; set; }

        public string? ProfilePhoto { get; set; }
    }
}