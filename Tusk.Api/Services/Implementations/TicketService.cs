using System.Linq.Expressions;
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

        private static readonly Expression<Func<Ticket, TicketDto>> ToDtoExpression = ticket => new TicketDto
        {
            Id = ticket.Id,
            Version = ticket.Version,
            Title = ticket.Title,
            Description = ticket.Description,
            Priority = ticket.Priority,
            Status = ticket.Status,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            ClosedAt = ticket.ClosedAt,
            ClosedById = ticket.ClosedById,
            ClosedByName = ticket.ClosedById == null ? null : (ticket.ClosedBy!.FirstName + " " + ticket.ClosedBy.LastName).Trim(),
            CloseReason = ticket.CloseReason,
            CustomerId = ticket.CustomerId,
            CustomerName = (ticket.Customer.FirstName + " " + ticket.Customer.LastName).Trim(),
            AssignedToId = ticket.AssignedToId,
            AssignedToName = ticket.AssignedToId == null ? null : (ticket.AssignedTo!.FirstName + " " + ticket.AssignedTo.LastName).Trim(),
            ProjectId = ticket.ProjectId
        };

        private async Task<TicketDto> ProjectTicketAsync(int ticketId)
        {
            return await _context.Tickets
                .Where(t => t.Id == ticketId)
                .Select(ToDtoExpression)
                .FirstAsync();
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
                Priority = dto.Priority!.Value,
                Status = TicketStatus.Open,
                CreatedAt = DateTime.UtcNow,
                CustomerId = customerId,
                ProjectId = projectId
            };

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            return await ProjectTicketAsync(ticket.Id);
        }

        private static async Task<PagedResultDto<TicketDto>> ToPagedResultAsync(IQueryable<Ticket> source, TicketQueryParams query)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            if (query.Status.HasValue)
                source = source.Where(t => t.Status == query.Status.Value);

            if (query.Priority.HasValue)
                source = source.Where(t => t.Priority == query.Priority.Value);

            if (!string.IsNullOrEmpty(query.AssignedToId))
                source = source.Where(t => t.AssignedToId == query.AssignedToId);

            var totalCount = await source.CountAsync();

            var items = await source
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToDtoExpression)
                .ToListAsync();

            return new PagedResultDto<TicketDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PagedResultDto<TicketDto>> GetAllTicketsAsync(int projectId, TicketQueryParams query, string? customerId)
        {
            var source = _context.Tickets.Where(t => t.ProjectId == projectId);

            if (customerId != null)
                source = source.Where(t => t.CustomerId == customerId);

            return await ToPagedResultAsync(source, query);
        }

        public async Task<TicketDto?> GetTicketByIdAsync(int projectId, int ticketId)
        {
            return await _context.Tickets
                .Where(t => t.Id == ticketId && t.ProjectId == projectId)
                .Select(ToDtoExpression)
                .FirstOrDefaultAsync();
        }

        public async Task<TicketDto?> UpdateTicketContentAsync(int projectId, int ticketId, UpdateTicketContentDto dto)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            _context.ExpectVersion(ticket, dto.Version!.Value);
            ticket.Title = dto.Title;
            ticket.Description = dto.Description;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await ProjectTicketAsync(ticket.Id);
        }

        public async Task<TicketDto?> UpdateTicketStatusAsync(int projectId, int ticketId, UpdateTicketStatusDto dto)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.Status = dto.Status!.Value;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await ProjectTicketAsync(ticket.Id);
        }

        public async Task<TicketDto?> UpdateTicketPriorityAsync(int projectId, int ticketId, UpdateTicketPriorityDto dto)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.Priority = dto.Priority!.Value;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await ProjectTicketAsync(ticket.Id);
        }

        public async Task<TicketDto?> AssignTicketAsync(int projectId, int ticketId, AssignTicketDto dto)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.AssignedToId = dto.AssignedToId;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await ProjectTicketAsync(ticket.Id);
        }

        public async Task<TicketDto?> TakeTicketAsync(int projectId, int ticketId, string userId)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.AssignedToId = userId;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await ProjectTicketAsync(ticket.Id);
        }

        public async Task<TicketDto?> UnassignTicketAsync(int projectId, int ticketId)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.AssignedToId = null;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await ProjectTicketAsync(ticket.Id);
        }

        public async Task<TicketDto?> CloseTicketAsync(int projectId, int ticketId, string closedById, CloseTicketDto dto)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            var now = DateTime.UtcNow;
            ticket.Status = TicketStatus.Closed;
            ticket.ClosedAt = now;
            ticket.ClosedById = closedById;
            ticket.CloseReason = dto.Reason;
            ticket.UpdatedAt = now;
            await _context.SaveChangesAsync();

            return await ProjectTicketAsync(ticket.Id);
        }

        public async Task<TicketDto?> ReopenTicketAsync(int projectId, int ticketId)
        {
            var ticket = await LocateTicketAsync(projectId, ticketId);
            if (ticket == null) return null;

            ticket.Status = TicketStatus.Open;
            ticket.ClosedAt = null;
            ticket.ClosedById = null;
            ticket.CloseReason = null;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await ProjectTicketAsync(ticket.Id);
        }

        public async Task<PagedResultDto<TicketDto>> GetMyTicketsAsync(string userId, TicketQueryParams query)
        {
            return await ToPagedResultAsync(_context.Tickets.Where(t => t.CustomerId == userId), query);
        }

        public async Task<PagedResultDto<TicketDto>> GetAssignedTicketsAsync(string userId, TicketQueryParams query)
        {
            return await ToPagedResultAsync(_context.Tickets.Where(t => t.AssignedToId == userId), query);
        }
    }
}