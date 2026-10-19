using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;
using Tusk.Api.Dtos.Comments;
using Tusk.Api.Models;
using Tusk.Api.Services.Interfaces;

namespace Tusk.Api.Services.Implementations
{
    public class CommentService : ICommentService
    {
        private readonly AppDbContext _context;

        public CommentService(AppDbContext context)
        {
            _context = context;
        }

        private static readonly Expression<Func<Comment, CommentDto>> ToDtoExpression = comment => new CommentDto
        {
            Id = comment.Id,
            TicketId = comment.TicketId,
            UserId = comment.UserId,
            Message = comment.Message,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        };

        private static readonly Func<Comment, CommentDto> MapToDto = ToDtoExpression.Compile();

        private async Task<Comment?> LocateCommentAsync(int ticketId, int commentId)
        {
            return await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.TicketId == ticketId);
        }

        public async Task<string?> GetTicketCustomerIdAsync(int ticketId)
        {
            return await _context.Tickets
                .Where(t => t.Id == ticketId)
                .Select(t => t.CustomerId)
                .FirstOrDefaultAsync();
        }

        public async Task<List<CommentDto>> GetCommentsForTicketAsync(int ticketId)
        {
            return await _context.Comments
                .Where(c => c.TicketId == ticketId)
                .OrderBy(c => c.CreatedAt)
                .Select(ToDtoExpression)
                .ToListAsync();
        }

        public async Task<CommentDto?> GetCommentAsync(int ticketId, int commentId)
        {
            var comment = await LocateCommentAsync(ticketId, commentId);
            if (comment == null) return null;

            return MapToDto(comment);
        }

        public async Task<CommentDto?> CreateCommentAsync(CreateCommentDto dto, string userId, int ticketId)
        {
            if (await _context.Tickets.AnyAsync(t => t.Id == ticketId) == false)
            {
                return null;
            }

            var comment = new Comment
            {
                TicketId = ticketId,
                UserId = userId,
                Message = dto.Message,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return MapToDto(comment);
        }

        public async Task<CommentDto?> UpdateCommentAsync(int ticketId, int commentId, UpdateCommentDto dto)
        {
            var comment = await LocateCommentAsync(ticketId, commentId);
            if (comment == null) return null;

            comment.Message = dto.Message;
            comment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToDto(comment);
        }

        public async Task<bool> DeleteCommentAsync(int ticketId, int commentId)
        {
            var comment = await LocateCommentAsync(ticketId, commentId);
            if (comment == null) return false;

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
