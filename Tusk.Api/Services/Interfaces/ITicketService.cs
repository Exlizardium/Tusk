using Tusk.Api.Dtos.Tickets;

namespace Tusk.Api.Services.Interfaces
{
    public interface ITicketService
    {
        Task<TicketDto> CreateTicketAsync(int projectId, CreateTicketDto dto, string userId);
        Task<List<TicketDto>> GetAllTicketsAsync(int projectId);
        Task<TicketDto?> GetTicketByIdAsync(int projectId, int ticketId);

        Task<TicketDto?> UpdateTicketContentAsync(int projectId, int ticketId, UpdateTicketContentDto dto);
        Task<TicketDto?> UpdateTicketStatusAsync(int projectId, int ticketId, UpdateTicketStatusDto dto);
        Task<TicketDto?> UpdateTicketPriorityAsync(int projectId, int ticketId, UpdateTicketPriorityDto dto);

        Task<TicketDto?> AssignTicketAsync(int projectId, int ticketId, AssignTicketDto dto);
        Task<TicketDto?> TakeTicketAsync(int projectId, int ticketId, string userId);
        Task<TicketDto?> UnassignTicketAsync(int projectId, int ticketId);

        Task<List<TicketDto>> GetMyTicketsAsync(string userId);
        Task<List<TicketDto>> GetAssignedTicketsAsync(string userId);
    }
}
