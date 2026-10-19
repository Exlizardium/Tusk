using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<ApplicationUser> _userManager;

        public TicketsController(ITicketService ticketService, UserManager<ApplicationUser> userManager)
        {
            _ticketService = ticketService;
            _userManager = userManager;
        }

        private string? GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        private bool IsAgentOrAdmin()
        {
            return User.IsInRole("Agent") || User.IsInRole("Admin");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll(int projectId, [FromQuery] TicketQueryParams query)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var customerFilter = IsAgentOrAdmin() ? null : userId;
            var tickets = await _ticketService.GetAllTicketsAsync(projectId, query, customerFilter);

            return Ok(tickets);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int projectId, int id)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var ticket = await _ticketService.GetTicketByIdAsync(projectId, id);

            if (ticket == null)
                return NotFound();

            if (ticket.CustomerId != userId && !IsAgentOrAdmin())
                return Forbid();

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

            return CreatedAtAction(nameof(GetById), new { projectId, id = createdTicket.Id }, createdTicket);
        }

        [Authorize]
        [HttpPut("{id}/content")]
        public async Task<IActionResult> UpdateContent(int projectId, int id, [FromBody] UpdateTicketContentDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var existing = await _ticketService.GetTicketByIdAsync(projectId, id);
            if (existing == null) return NotFound();

            if (existing.CustomerId != userId && !IsAgentOrAdmin())
                return Forbid();

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

            var assignee = await _userManager.FindByIdAsync(dto.AssignedToId);
            if (assignee == null)
                return BadRequest("Assignee not found");

            if (!await _userManager.IsInRoleAsync(assignee, "Agent") && !await _userManager.IsInRoleAsync(assignee, "Admin"))
                return BadRequest("Assignee must be an Agent or Admin");

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