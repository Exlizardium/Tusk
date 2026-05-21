using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;
using Tusk.Api.Dtos;
using Tusk.Api.Models;
using Tusk.Api.Models.Enums;
using Tusk.Api.Services.Interfaces;

namespace Tusk.Api.Services.Implementations
{
    public class TicketService : ITicketService
    {
        private readonly AppDbContext _context;

        public TicketService(AppDbContext context)
        {
            _context = context;
        }

        private async Task<Ticket?> GetTicketForCustomerAsync(int ticketId, string userId)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);

            if (ticket == null) return null;
            if (ticket.CustomerId != userId) return null;

            return ticket;
        }

        private static TicketDto MapToDto(Ticket ticket)
        {
            return new TicketDto
            {
                Id = ticket.Id,
                Title = ticket.Title,
                Description = ticket.Description,
                Priority = ticket.Priority,
                Status = ticket.Status,
                CreatedAt = ticket.CreatedAt,
                CustomerId = ticket.CustomerId
            };
        }

        public async Task<TicketDto> CreateTicketAsync(CreateTicketDto dto, string customerId)
        {
            var Ticket = new Ticket
            {
                Title = dto.Title,
                Description = dto.Description,
                Priority = dto.Priority,
                Status = TicketStatus.Open,
                CreatedAt = DateTime.UtcNow,
                CustomerId = customerId
            };

            _context.Tickets.Add(Ticket);
            await _context.SaveChangesAsync();

            return MapToDto(Ticket);
        }

        public async Task<List<TicketDto>> GetAllTicketsAsync()
        {
            return await _context.Tickets
                .Select(t => new TicketDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Priority = t.Priority,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    CustomerId = t.CustomerId
                })
                .ToListAsync();
        }

        public async Task<TicketDto?> GetTicketByIdAsync(int ticketId)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null) return null;

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> UpdateTicketContentAsync(int ticketId, UpdateTicketContentDto dto, string userId)
        {
            var ticket = await GetTicketForCustomerAsync(ticketId, userId);

            ticket.Title = dto.Title;
            ticket.Description = dto.Description;
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> UpdateTicketStatusAsync(int ticketId, UpdateTicketStatusDto dto, string userId)
        {
            var ticket = await GetTicketForCustomerAsync(ticketId, userId);

            ticket.Status = dto.Status;
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> UpdateTicketPriorityAsync(int ticketId, UpdateTicketPriorityDto dto, string userId)
        {
            var ticket = await GetTicketForCustomerAsync(ticketId, userId);

            ticket.Priority = dto.Priority;
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> AssignTicketAsync(int ticketId, AssignTicketDto dto, string userId)
        {
            var ticket = await GetTicketForCustomerAsync(ticketId, userId);

            ticket.AssignedToId = dto.AssignedToId;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<List<TicketDto>> GetMyTicketsAsync(string userId)
        {
            return await _context.Tickets
                .Where(t => t.CustomerId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TicketDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Priority = t.Priority,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    CustomerId = t.CustomerId,
                    AssignedToId = t.AssignedToId
                })
                .ToListAsync();
        }

        public async Task<List<TicketDto>> GetAssignedTicketsAsync(string userId)
        {
            return await _context.Tickets
                .Where(t => t.AssignedToId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TicketDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Priority = t.Priority,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    CustomerId = t.CustomerId,
                    AssignedToId = t.AssignedToId
                })
                .ToListAsync();
        }
    }
}