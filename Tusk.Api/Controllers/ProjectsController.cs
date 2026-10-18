using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tusk.Api.Dtos.Projects;
using Tusk.Api.Services.Implementations;
using Tusk.Api.Services.Interfaces;


namespace Tusk.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;

        public ProjectsController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        private string? GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        private bool IsAdmin()
        {
            return User.IsInRole("Admin");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            var projects = await _projectService.GetAllProjectsAsync();
            return Ok(projects);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProject(int id)
        {
            var project = await _projectService.GetProjectByIdAsync(id);
            if (project == null) return NotFound();
            return Ok(project);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var project = await _projectService.CreateProjectAsync(dto, userId);
            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var existing = await _projectService.GetProjectByIdAsync(id);
            if (existing == null) return NotFound();

            if (existing.CreatedById != userId && !IsAdmin())
                return Forbid();

            var updatedProject = await _projectService.UpdateProjectAsync(id, dto);
            if (updatedProject == null) return NotFound();
            return Ok(updatedProject);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(int id)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var existing = await _projectService.GetProjectByIdAsync(id);
            if (existing == null) return NotFound();

            if (existing.CreatedById != userId && !IsAdmin())
                return Forbid();

            var deleted = await _projectService.DeleteProjectAsync(id);
            if (!deleted) return NotFound();

            return NoContent();
        }
    }
}
