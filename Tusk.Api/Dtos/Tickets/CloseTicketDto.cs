using System.ComponentModel.DataAnnotations;
using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos.Tickets
{
    public class CloseTicketDto
    {
        [Required]
        [EnumDataType(typeof(TicketCloseReason))]
        public TicketCloseReason? Reason { get; set; }
    }
}
