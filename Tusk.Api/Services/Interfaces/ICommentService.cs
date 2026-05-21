using Tusk.Api.Dtos.Comments;

namespace Tusk.Api.Services.Interfaces
{
    public interface ICommentService
    {
        Task<List<CommentDto>> GetCommentsForTicketAsync(int ticketId);
        Task<CommentDto> CreateCommentAsync(CreateCommentDto dto, string userId, int ticketId);
    }
}
