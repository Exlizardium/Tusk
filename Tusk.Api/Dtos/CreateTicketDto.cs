using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos
{
    public class CreateTicketDto
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public TicketPriority Priority { get; set; }
    }
}
