namespace Tusk.Api.Models
{
    public class Project
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
        public string CreatedById { get; set; } = null!;
        public ApplicationUser CreatedBy { get; set; } = null!;

        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}
