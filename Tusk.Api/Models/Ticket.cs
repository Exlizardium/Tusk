using Tusk.Api.Models.Enums;

namespace Tusk.Api.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public TicketStatus Status { get; set; }
        public TicketPriority Priority { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public DateTime? ClosedAt { get; set; }
        public string? ClosedById { get; set; }
        public ApplicationUser? ClosedBy { get; set; }
        public TicketCloseReason? CloseReason { get; set; }

        public string CustomerId { get; set; } = null!;
        public ApplicationUser Customer { get; set; } = null!;

        public string? AssignedToId { get; set; }
        public ApplicationUser? AssignedTo { get; set; }

        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    }
}
