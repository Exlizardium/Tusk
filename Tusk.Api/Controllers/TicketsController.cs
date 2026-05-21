using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tusk.Api.Dtos;
using Tusk.Api.Services.Implementations;
using Tusk.Api.Services.Interfaces;

namespace Tusk.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;

        public TicketsController(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        private string? GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tickets = await _ticketService.GetAllTicketsAsync();
            return Ok(tickets);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(id);
            if (ticket == null)
                return NotFound();
            return Ok(ticket);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTicketDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var createdTicket = await _ticketService.CreateTicketAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = createdTicket.Id }, createdTicket);
        }

        [Authorize]
        [HttpPut("{id}/content")]
        public async Task<IActionResult> UpdateContent(int id, [FromBody] UpdateTicketContentDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.UpdateTicketContentAsync(id, dto, userId);
            if (updatedTicket == null)
                return NotFound();
            return Ok(updatedTicket);
        }

        [Authorize(Roles = "Agent,Admin")]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTicketStatusDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.UpdateTicketStatusAsync(id, dto, userId);
            if (updatedTicket == null)
                return NotFound();
            return Ok(updatedTicket);
        }

        [Authorize(Roles = "Agent,Admin")]
        [HttpPut("{id}/priority")]
        public async Task<IActionResult> UpdatePriority(int id, [FromBody] UpdateTicketPriorityDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.UpdateTicketPriorityAsync(id, dto, userId);
            if (updatedTicket == null)
                return NotFound();
            return Ok(updatedTicket);
        }

        [Authorize(Roles = "Agent,Admin")]
        [HttpPut("{id}/assign")]
        public async Task<IActionResult> AssignTicket(int id, [FromBody] AssignTicketDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.AssignTicketAsync(id, dto, userId);
            if (updatedTicket == null)
                return NotFound();

            return Ok(updatedTicket);
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