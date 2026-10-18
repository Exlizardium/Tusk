using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;
using Tusk.Api.Dtos.Common;
using Tusk.Api.Dtos.Tickets;
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

        private async Task<Ticket?> LocateTicketAsync(int projectId, int ticketId)
        {
            var ticket = await _context.Tickets
                .FirstOrDefaultAsync(t => t.Id == ticketId && t.ProjectId == projectId);
            if (ticket == null) return null;
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
                CustomerId = ticket.CustomerId,
                AssignedToId = ticket.AssignedToId,
                ProjectId = ticket.ProjectId
            };
        }

        public async Task<TicketDto> CreateTicketAsync(int projectId, CreateTicketDto dto, string customerId)
        {
            var projectExists = await _context.Projects
                .AnyAsync(p => p.Id == projectId);

            if (!projectExists)
                throw new KeyNotFoundException("Project not found");

            var ticket = new Ticket
            {
                Title = dto.Title,
                Description = dto.Description,
                Priority = dto.Priority,
                Status = TicketStatus.Open,
                CreatedAt = DateTime.UtcNow,
                CustomerId = customerId,
                ProjectId = projectId
            };

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<PagedResultDto<TicketDto>> GetAllTicketsAsync(int projectId, TicketQueryParams query)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var filtered = _context.Tickets.Where(t => t.ProjectId == projectId);

            if (query.Status.HasValue)
                filtered = filtered.Where(t => t.Status == query.Status.Value);

            if (query.Priority.HasValue)
                filtered = filtered.Where(t => t.Priority == query.Priority.Value);

            if (!string.IsNullOrEmpty(query.AssignedToId))
                filtered = filtered.Where(t => t.AssignedToId == query.AssignedToId);

            var totalCount = await filtered.CountAsync();

            var items = await filtered
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TicketDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Priority = t.Priority,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    CustomerId = t.CustomerId,
                    AssignedToId = t.AssignedToId,
                    ProjectId = t.ProjectId
                })
                .ToListAsync();

            return new PagedResultDto<TicketDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<TicketDto?> GetTicketByIdAsync(int projectId, int ticketId)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> UpdateTicketContentAsync(int projectId, int ticketId, UpdateTicketContentDto dto)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.Title = dto.Title;
            ticket.Description = dto.Description;
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> UpdateTicketStatusAsync(int projectId, int ticketId, UpdateTicketStatusDto dto)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.Status = dto.Status;
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> UpdateTicketPriorityAsync(int projectId, int ticketId, UpdateTicketPriorityDto dto)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.Priority = dto.Priority;
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> AssignTicketAsync(int projectId, int ticketId, AssignTicketDto dto)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.AssignedToId = dto.AssignedToId;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> TakeTicketAsync(int projectId, int ticketId, string userId)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.AssignedToId = userId;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToDto(ticket);
        }

        public async Task<TicketDto?> UnassignTicketAsync(int projectId, int ticketId)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.AssignedToId = null;
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
                    AssignedToId = t.AssignedToId,
                    ProjectId = t.ProjectId
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
                    AssignedToId = t.AssignedToId,
                    ProjectId = t.ProjectId
                })
                .ToListAsync();
        }
    }
}