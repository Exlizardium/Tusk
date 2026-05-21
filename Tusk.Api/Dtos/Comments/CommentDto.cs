using Tusk.Api.Models;

namespace Tusk.Api.Dtos.Comments
{
    public class CommentDto
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public string UserId { get; set; } = null!;

        public string Message { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
