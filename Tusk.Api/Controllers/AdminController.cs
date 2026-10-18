using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Admin;
using Tusk.Api.Dtos.Common;
using Tusk.Api.Models;

namespace Tusk.Api.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpPost("assign-role")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleDto assignRoleDto)
        {
            var user = await _userManager.FindByIdAsync(assignRoleDto.userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            var roleName = assignRoleDto.RoleName.ToString();

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return NotFound("Role not found");
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok($"Role {role.Name} assigned successfully to {user.UserName}");
        }

        [HttpGet("users-with-roles")]
        public async Task<IActionResult> GetUsersWithRoles([FromQuery] UserQueryParams query)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var totalCount = await _userManager.Users.CountAsync();

            var users = await _userManager.Users
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var usersWithRoles = new List<UsersWithRolesDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                usersWithRoles.Add(new UsersWithRolesDto
                {
                    Id = user.Id,
                    FullName = $"{user.FirstName} {user.LastName}",
                    Email = user.Email!,
                    Roles = roles.ToList()
                });
            }

            var result = new PagedResultDto<UsersWithRolesDto>
            {
                Items = usersWithRoles,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(result);
        }
    }
}