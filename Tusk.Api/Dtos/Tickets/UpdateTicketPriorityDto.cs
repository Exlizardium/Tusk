using System.ComponentModel.DataAnnotations;
using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos.Tickets
{
    public class UpdateTicketPriorityDto
    {
        [EnumDataType(typeof(TicketPriority))]
        public TicketPriority Priority { get; set; }
    }
}
