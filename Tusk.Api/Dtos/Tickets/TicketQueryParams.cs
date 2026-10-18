using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos.Tickets
{
    public class TicketQueryParams
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public TicketStatus? Status { get; set; }
        public TicketPriority? Priority { get; set; }
        public string? AssignedToId { get; set; }
    }
}
