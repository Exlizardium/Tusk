using System.ComponentModel.DataAnnotations;

namespace Tusk.Api.Dtos.Tickets
{
    public class AssignTicketDto
    {
        [Required]
        public string AssignedToId { get; set; } = null!;
    }
}
