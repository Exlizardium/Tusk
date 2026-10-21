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

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetProjects([FromQuery] ProjectQueryParams query)
        {
            var projects = await _projectService.GetAllProjectsAsync(query);
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

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var project = await _projectService.CreateProjectAsync(dto, userId);
            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectDto dto)
        {
            var existing = await _projectService.GetProjectByIdAsync(id);
            if (existing == null) return NotFound();

            if (existing.Version != dto.Version)
                return Conflict(existing);

            var updatedProject = await _projectService.UpdateProjectAsync(id, dto);
            if (updatedProject == null) return NotFound();
            return Ok(updatedProject);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(int id)
        {
            var deleted = await _projectService.DeleteProjectAsync(id);
            if (!deleted) return NotFound();

            return NoContent();
        }
    }
}
