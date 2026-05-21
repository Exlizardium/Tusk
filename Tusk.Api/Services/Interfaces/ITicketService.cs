using Tusk.Api.Dtos;

namespace Tusk.Api.Services.Interfaces
{
    public interface ITicketService
    {
        Task<TicketDto> CreateTicketAsync(CreateTicketDto dto, string userId);
        Task<List<TicketDto>> GetAllTicketsAsync();
        Task<TicketDto?> GetTicketByIdAsync(int ticketId);

        Task<TicketDto?> UpdateTicketContentAsync(int ticketId, UpdateTicketContentDto dto, string userId);
        Task<TicketDto?> UpdateTicketStatusAsync(int ticketId, UpdateTicketStatusDto dto, string userId);
        Task<TicketDto?> UpdateTicketPriorityAsync(int ticketId, UpdateTicketPriorityDto dto, string userId);
        Task<TicketDto?> AssignTicketAsync(int ticketId, AssignTicketDto dto, string userId);
    }
}
