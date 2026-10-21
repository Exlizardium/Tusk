using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Comments;
using Tusk.Api.Dtos.Projects;
using Tusk.Api.Dtos.Tickets;
using Tusk.Api.Models.Enums;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class ValidationTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        private static Task<HttpResponseMessage> PostRawJsonAsync(HttpClient client, string url, string json) =>
            client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

        private static Task<HttpResponseMessage> PutRawJsonAsync(HttpClient client, string url, string json) =>
            client.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"name\":\"\"}")]
        [InlineData("{\"name\":\"   \"}")]
        public async Task CreateProject_Returns400_WhenNameMissingOrBlank(string json)
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await PostRawJsonAsync(admin.Client, "/api/projects", json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Projects.AnyAsync()));
        }

        [Theory]
        [InlineData(201, 0)]
        [InlineData(10, 251)]
        public async Task CreateProject_Returns400_WhenFieldsExceedColumnLength(int nameLength, int descriptionLength)
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await admin.Client.PostAsJsonAsync("/api/projects", new CreateProjectDto
            {
                Name = new string('n', nameLength),
                Description = new string('d', descriptionLength)
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Projects.AnyAsync()));
        }

        [Fact]
        public async Task CreateProject_Accepts_FieldsAtExactColumnLength()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await admin.Client.PostAsJsonAsync("/api/projects", new CreateProjectDto
            {
                Name = new string('n', 200),
                Description = new string('d', 250)
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task UpdateProject_Returns400_WhenNameBlank()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);

            var response = await PutRawJsonAsync(admin.Client, $"/api/projects/{project.Id}", "{\"name\":\"\"}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Alpha", await _factory.QueryAsync(db => db.Projects.Select(p => p.Name).SingleAsync()));
        }

        [Theory]
        [InlineData("{\"priority\":1}")]
        [InlineData("{\"title\":\"  \",\"priority\":1}")]
        [InlineData("{\"title\":\"Broken\"}")]
        [InlineData("{\"title\":\"Broken\",\"priority\":99}")]
        [InlineData("{\"title\":\"Broken\",\"priority\":-1}")]
        public async Task CreateTicket_Returns400_ForMissingTitleOrUndefinedPriority(string json)
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            var customer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await PostRawJsonAsync(customer.Client, $"/api/projects/{project.Id}/tickets", json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Tickets.AnyAsync()));
        }

        [Theory]
        [InlineData(201, 0)]
        [InlineData(10, 4001)]
        public async Task CreateTicket_Returns400_WhenFieldsTooLong(int titleLength, int descriptionLength)
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            var customer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await customer.Client.PostAsJsonAsync($"/api/projects/{project.Id}/tickets", new CreateTicketDto
            {
                Title = new string('t', titleLength),
                Description = new string('d', descriptionLength),
                Priority = TicketPriority.Medium
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Tickets.AnyAsync()));
        }

        [Fact]
        public async Task UpdateTicketContent_Returns400_WhenTitleTooLong()
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id, "Original");

            var response = await customer.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/{ticket.Id}/content", new UpdateTicketContentDto { Title = new string('t', 201) });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Original", await _factory.QueryAsync(db => db.Tickets.Select(t => t.Title).SingleAsync()));
        }

        [Theory]
        [InlineData("status", "{\"status\":99}")]
        [InlineData("status", "{}")]
        [InlineData("priority", "{\"priority\":99}")]
        [InlineData("priority", "{}")]
        [InlineData("assign", "{}")]
        [InlineData("assign", "{\"assignedToId\":\"\"}")]
        public async Task StaffTicketActions_Return400_ForInvalidBody(string action, string json)
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await PutRawJsonAsync(agent.Client, $"/api/projects/{project.Id}/tickets/{ticket.Id}/{action}", json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var saved = await _factory.QueryAsync(db => db.Tickets.SingleAsync());
            Assert.Equal(TicketStatus.Open, saved.Status);
            Assert.Equal(TicketPriority.Medium, saved.Priority);
            Assert.Null(saved.AssignedToId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4001)]
        public async Task CreateComment_Returns400_WhenMessageEmptyOrTooLong(int messageLength)
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var response = await customer.Client.PostAsJsonAsync(
                $"/api/tickets/{ticket.Id}/comments", new CreateCommentDto { Message = new string('m', messageLength) });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Comments.AnyAsync()));
        }

        [Theory]
        [InlineData("{\"firstName\":\"Jane\",\"lastName\":\"Doe\",\"email\":\"not-an-email\",\"password\":\"Passw0rd!\"}")]
        [InlineData("{\"firstName\":\"\",\"lastName\":\"Doe\",\"email\":\"jane@test.local\",\"password\":\"Passw0rd!\"}")]
        [InlineData("{\"firstName\":\"Jane\",\"email\":\"jane@test.local\",\"password\":\"Passw0rd!\"}")]
        [InlineData("{\"firstName\":\"Jane\",\"lastName\":\"Doe\",\"email\":\"jane@test.local\"}")]
        public async Task Register_Returns400_ForInvalidBody(string json)
        {
            var response = await PostRawJsonAsync(_factory.CreateClient(), "/api/auth/register", json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Users.AnyAsync()));
        }

        [Fact]
        public async Task Register_Returns400_WhenNameTooLong()
        {
            var json = $"{{\"firstName\":\"{new string('f', 101)}\",\"lastName\":\"Doe\",\"email\":\"jane@test.local\",\"password\":\"Passw0rd!\"}}";

            var response = await PostRawJsonAsync(_factory.CreateClient(), "/api/auth/register", json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Users.AnyAsync()));
        }

        [Theory]
        [InlineData("{\"password\":\"Passw0rd!\"}")]
        [InlineData("{\"email\":\"\",\"password\":\"Passw0rd!\"}")]
        [InlineData("{\"email\":\"jane@test.local\"}")]
        public async Task Login_Returns400_WhenFieldsMissing(string json)
        {
            var response = await PostRawJsonAsync(_factory.CreateClient(), "/api/auth/login", json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("{\"roleName\":1}")]
        [InlineData("{\"userId\":\"\",\"roleName\":1}")]
        [InlineData("{\"userId\":\"someone\",\"roleName\":99}")]
        public async Task AssignRole_Returns400_ForInvalidBody(string json)
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await PostRawJsonAsync(admin.Client, "/api/admin/assign-role", json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task AssignRole_Returns400_AndAddsNoRole_WhenRoleNameMissing()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            var agent = await _factory.AddUserAsync("Agent");

            var response = await PostRawJsonAsync(admin.Client, "/api/admin/assign-role", $"{{\"userId\":\"{agent.Id}\"}}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var roles = await _factory.QueryAsync(db =>
                (from userRole in db.UserRoles
                 join role in db.Roles on userRole.RoleId equals role.Id
                 where userRole.UserId == agent.Id
                 select role.Name).ToListAsync());
            Assert.Equal(["Agent"], roles);
        }
    }
}
