using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;
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
        public const IsolationLevel RoleChangeIsolationLevel = IsolationLevel.Serializable;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [HttpPut("users/{userId}/role")]
        public async Task<IActionResult> SetRole(string userId, [FromBody] SetUserRoleDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            var roleName = dto.Role!.Value.ToString();

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return NotFound("Role not found");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(RoleChangeIsolationLevel);

            var currentRoles = await _userManager.GetRolesAsync(user);

            if (currentRoles.Contains("Admin") && roleName != "Admin"
                && (await _userManager.GetUsersInRoleAsync("Admin")).Count == 1)
            {
                return Conflict("Cannot demote the last Admin");
            }

            var removed = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removed.Succeeded)
            {
                return BadRequest(removed.Errors);
            }

            var added = await _userManager.AddToRoleAsync(user, roleName);
            if (!added.Succeeded)
            {
                return BadRequest(added.Errors);
            }

            await transaction.CommitAsync();

            return Ok(new UsersWithRolesDto
            {
                Id = user.Id,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email!,
                Roles = [roleName]
            });
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

            var userIds = users.Select(u => u.Id).ToList();

            var rolesByUserId = (await _context.UserRoles
                    .Where(ur => userIds.Contains(ur.UserId))
                    .Join(_context.Roles,
                        ur => ur.RoleId,
                        r => r.Id,
                        (ur, r) => new { ur.UserId, RoleName = r.Name! })
                    .ToListAsync())
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

            var usersWithRoles = users.Select(user => new UsersWithRolesDto
            {
                Id = user.Id,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email!,
                Roles = rolesByUserId.TryGetValue(user.Id, out var roles) ? roles : new List<string>()
            }).ToList();

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