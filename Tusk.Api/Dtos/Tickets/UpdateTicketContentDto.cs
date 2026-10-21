using System.ComponentModel.DataAnnotations;

namespace Tusk.Api.Dtos.Tickets
{
    public class UpdateTicketContentDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(4000)]
        public string? Description { get; set; }

        [Required]
        public int? Version { get; set; }
    }
}
