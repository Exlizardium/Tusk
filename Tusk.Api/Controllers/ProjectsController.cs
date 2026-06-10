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
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto, string userId)
        {
            var project = await _projectService.CreateProjectAsync(dto, userId);
            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectDto dto)
        {
            var updatedProject = await _projectService.UpdateProjectAsync(id, dto);
            if (updatedProject == null) return NotFound();
            return Ok(updatedProject);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(int id)
        {
            var targetProject = await _projectService.DeleteProjectAsync(id);
            
            return Ok(targetProject);
        }
    }
}
