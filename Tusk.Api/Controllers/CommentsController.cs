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

            return CreatedAtAction(nameof(GetComment), new { ticketId, id = createdComment.Id }, createdComment);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetComment(int ticketId, int id)
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

            var comment = await _commentService.GetCommentAsync(ticketId, id);
            if (comment == null)
            {
                return NotFound("Comment not found");
            }

            return Ok(comment);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateComment(int ticketId, int id, [FromBody] UpdateCommentDto dto)
        {
            var userId = GetUserId();

            var existing = await _commentService.GetCommentAsync(ticketId, id);
            if (existing == null)
            {
                return NotFound();
            }

            if (existing.UserId != userId)
            {
                return Forbid();
            }

            var updatedComment = await _commentService.UpdateCommentAsync(ticketId, id, dto);
            if (updatedComment == null)
            {
                return NotFound();
            }

            return Ok(updatedComment);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(int ticketId, int id)
        {
            var userId = GetUserId();

            var existing = await _commentService.GetCommentAsync(ticketId, id);
            if (existing == null)
            {
                return NotFound();
            }

            if (existing.UserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var deleted = await _commentService.DeleteCommentAsync(ticketId, id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
