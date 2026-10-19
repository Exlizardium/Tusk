using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tusk.Api.Dtos.Comments;
using Tusk.Api.Models;
using Tusk.Api.Services.Interfaces;

namespace Tusk.Api.Controllers
{
    [Route("api/tickets/{ticketId}/comments")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentService _commentService;

        public CommentsController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new Exception("User ID not found");
        }

        private bool IsAgentOrAdmin()
        {
            return User.IsInRole("Agent") || User.IsInRole("Admin");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetCommentsForTicket(int ticketId)
        {
            var userId = GetUserId();

            var ticketCustomerId = await _commentService.GetTicketCustomerIdAsync(ticketId);
            if (ticketCustomerId == null)
            {
                return NotFound("Ticket not found");
            }

            if (ticketCustomerId != userId && !IsAgentOrAdmin())
            {
                return Forbid();
            }

            var comments = await _commentService.GetCommentsForTicketAsync(ticketId);
            return Ok(comments);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateCommentAsync(int ticketId, [FromBody] CreateCommentDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var ticketCustomerId = await _commentService.GetTicketCustomerIdAsync(ticketId);
            if (ticketCustomerId == null)
            {
                return NotFound("Ticket not found");
            }

            if (ticketCustomerId != userId && !IsAgentOrAdmin())
            {
                return Forbid();
            }

            var createdComment = await _commentService.CreateCommentAsync(dto, userId, ticketId);
            if (createdComment == null)
            {
                return NotFound("Ticket not found");
            }

            return Ok(createdComment);
        }
    }
}
