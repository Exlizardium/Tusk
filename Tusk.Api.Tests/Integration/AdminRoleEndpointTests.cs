using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Admin;
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
            var body = await response.Content.ReadFromJsonAsync<UsersWithRolesDto>();
            Assert.Equal(user.Id, body!.Id);
            Assert.Equal([newRole.ToString()], body.Roles);
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
