using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tusk.Api.Dtos.Admin;
using Tusk.Api.Dtos.Common;
using Tusk.Api.Models;
using Tusk.Api.Models.Enums;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class AdminRoleEndpointTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        private static string RoleUrl(string userId) => $"/api/admin/users/{userId}/role";

        private static SetUserRoleDto To(RoleType role) => new() { Role = role };

        private Task<List<string?>> RolesOfAsync(string userId) =>
            _factory.QueryAsync(db =>
                (from userRole in db.UserRoles
                 join role in db.Roles on userRole.RoleId equals role.Id
                 where userRole.UserId == userId
                 select role.Name).ToListAsync());

        [Theory]
        [InlineData("Agent", RoleType.Customer)]
        [InlineData("Customer", RoleType.Agent)]
        [InlineData("Customer", RoleType.Admin)]
        [InlineData("Agent", RoleType.Agent)]
        public async Task SetRole_LeavesUserWithExactlyTheNewRole(string currentRole, RoleType newRole)
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            var user = await _factory.AddUserAsync(currentRole);

            var response = await admin.Client.PutAsJsonAsync(RoleUrl(user.Id), To(newRole));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal([newRole.ToString()], await RolesOfAsync(user.Id));
            var body = await response.Content.ReadFromJsonAsync<UserWithRoleDto>();
            Assert.Equal(user.Id, body!.Id);
            Assert.Equal(newRole, body.Role);
        }

        [Fact]
        public async Task GetUsersWithRoles_ReturnsEachUsersSingleRole_HighestForLegacyMultiRoleUsers()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            var customer = await _factory.AddUserAsync("Customer");
            var legacyAgent = await _factory.AddUserAsync("Agent");
            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                await users.AddToRoleAsync((await users.FindByIdAsync(legacyAgent.Id))!, "Customer");
            }

            var page = await admin.Client.GetFromJsonAsync<PagedResultDto<UserWithRoleDto>>("/api/admin/users-with-roles");

            var roleById = page!.Items.ToDictionary(u => u.Id, u => u.Role);
            Assert.Equal(RoleType.Admin, roleById[admin.Id]);
            Assert.Equal(RoleType.Customer, roleById[customer.Id]);
            Assert.Equal(RoleType.Agent, roleById[legacyAgent.Id]);
        }

        [Fact]
        public async Task SetRole_Returns409_WhenDemotingTheLastAdmin()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await admin.Client.PutAsJsonAsync(RoleUrl(admin.Id), To(RoleType.Agent));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(["Admin"], await RolesOfAsync(admin.Id));
        }

        [Fact]
        public async Task SetRole_AllowsAdminToStepDown_WhenAnotherAdminExists()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            await _factory.AddUserAsync("Admin");

            var response = await admin.Client.PutAsJsonAsync(RoleUrl(admin.Id), To(RoleType.Customer));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(["Customer"], await RolesOfAsync(admin.Id));
        }

        [Fact]
        public async Task SetRole_Returns404_WhenUserDoesNotExist()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await admin.Client.PutAsJsonAsync(RoleUrl("no-such-user"), To(RoleType.Agent));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task SetRole_Returns403_ForAgent()
        {
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var customer = await _factory.AddUserAsync("Customer");

            var response = await agent.Client.PutAsJsonAsync(RoleUrl(customer.Id), To(RoleType.Admin));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(["Customer"], await RolesOfAsync(customer.Id));
        }

        [Fact]
        public async Task AssignRoleRoute_IsGone()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            var customer = await _factory.AddUserAsync("Customer");

            var response = await admin.Client.PostAsJsonAsync("/api/admin/assign-role", new { userId = customer.Id, roleName = 1 });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(["Customer"], await RolesOfAsync(customer.Id));
        }
    }
}
