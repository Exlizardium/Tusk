using System.ComponentModel.DataAnnotations;
using Tusk.Api.Models.Enums;

namespace Tusk.Api.Dtos.Tickets
{
    public class UpdateTicketStatusDto
    {
        [EnumDataType(typeof(TicketStatus))]
        public TicketStatus Status { get; set; }
    }
}
