using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tusk.Api.Dtos.Tickets;
using Tusk.Api.Models;
using Tusk.Api.Services.Implementations;
using Tusk.Api.Services.Interfaces;

namespace Tusk.Api.Controllers
{
    [Route("api/projects/{projectId}/tickets")]
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

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll(int projectId)
        {
            var tickets = await _ticketService.GetAllTicketsAsync(projectId);

            return Ok(tickets);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int projectId, int id)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(projectId, id);

            if (ticket == null)
                return NotFound();

            return Ok(ticket);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(int projectId, [FromBody] CreateTicketDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var createdTicket = await _ticketService.CreateTicketAsync(projectId, dto, userId);

            return CreatedAtAction(nameof(GetById), new { id = createdTicket.Id }, createdTicket);
        }

        [Authorize]
        [HttpPut("{id}/content")]
        public async Task<IActionResult> UpdateContent(int projectId, int id, [FromBody] UpdateTicketContentDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.UpdateTicketContentAsync(projectId, id, dto);
            if (updatedTicket == null)
                return NotFound();
            return Ok(updatedTicket);
        }

        [Authorize(Roles = "Agent,Admin")]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int projectId, int id, [FromBody] UpdateTicketStatusDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.UpdateTicketStatusAsync(projectId, id, dto);
            if (updatedTicket == null)
                return NotFound();
            return Ok(updatedTicket);
        }

        [Authorize(Roles = "Agent,Admin")]
        [HttpPut("{id}/priority")]
        public async Task<IActionResult> UpdatePriority(int projectId, int id, [FromBody] UpdateTicketPriorityDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.UpdateTicketPriorityAsync(projectId, id, dto);
            if (updatedTicket == null)
                return NotFound();
            return Ok(updatedTicket);
        }

        [Authorize(Roles = "Agent,Admin")]
        [HttpPut("{id}/assign")]
        public async Task<IActionResult> AssignTicket(int projectId, int id, [FromBody] AssignTicketDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.AssignTicketAsync(projectId, id, dto);
            if (updatedTicket == null)
                return NotFound();

            return Ok(updatedTicket);
        }

        [Authorize(Roles = "Agent,Admin")]
        [HttpPut("{id}/take")]
        public async Task<IActionResult> TakeTicket(int projectId, int id)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.TakeTicketAsync(projectId, id, userId);
            if (updatedTicket == null)
                return NotFound();

            return Ok(updatedTicket);
        }

        [Authorize(Roles = "Agent,Admin")]
        [HttpPut("{id}/unassign")]
        public async Task<IActionResult> UnassignTicket(int projectId, int id)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var updatedTicket = await _ticketService.UnassignTicketAsync(projectId, id);
            if (updatedTicket == null)
                return NotFound();

            return Ok(updatedTicket);
        }
    }
}