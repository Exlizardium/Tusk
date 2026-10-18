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

        public async Task<List<CommentDto>> GetCommentsForTicketAsync(int ticketId)
        {
            return await _context.Comments
                .Where(c => c.TicketId == ticketId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    TicketId = c.TicketId,
                    UserId = c.UserId,
                    Message = c.Message,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();
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

            return new CommentDto
            {
                Id = comment.Id,
                TicketId = comment.TicketId,
                UserId = comment.UserId,
                Message = comment.Message,
                CreatedAt = comment.CreatedAt
            };
        }
    }
}
