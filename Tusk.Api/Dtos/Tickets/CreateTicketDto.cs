using System.ComponentModel.DataAnnotations;
using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos.Tickets
{
    public class CreateTicketDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(4000)]
        public string? Description { get; set; }

        [EnumDataType(typeof(TicketPriority))]
        public TicketPriority Priority { get; set; }
    }
}
