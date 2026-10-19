using Tusk.Api.Dtos.Comments;

namespace Tusk.Api.Services.Interfaces
{
    public interface ICommentService
    {
        Task<string?> GetTicketCustomerIdAsync(int ticketId);
        Task<List<CommentDto>> GetCommentsForTicketAsync(int ticketId);
        Task<CommentDto?> GetCommentAsync(int ticketId, int commentId);
        Task<CommentDto?> CreateCommentAsync(CreateCommentDto dto, string userId, int ticketId);
        Task<CommentDto?> UpdateCommentAsync(int ticketId, int commentId, UpdateCommentDto dto);
        Task<bool> DeleteCommentAsync(int ticketId, int commentId);
    }
}
