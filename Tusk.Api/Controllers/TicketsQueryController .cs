using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tusk.Api.Dtos.Tickets;
using Tusk.Api.Services.Implementations;
using Tusk.Api.Services.Interfaces;

namespace Tusk.Api.Controllers
{
    [Route("api/tickets")]
    [ApiController]
    public class TicketsQueryController : ControllerBase
    {
        private readonly ITicketService _ticketService;

        public TicketsQueryController(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        private string? GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        [Authorize]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMyTickets()
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var tickets = await _ticketService.GetMyTicketsAsync(userId);

            return Ok(tickets);
        }

        [Authorize(Roles = "Agent,Admin")]
        [HttpGet("assigned")]
        public async Task<IActionResult> GetAssignedTickets()
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var tickets = await _ticketService.GetAssignedTicketsAsync(userId);

            return Ok(tickets);
        }
    }
}
