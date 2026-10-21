using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Auth;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class AdminSeedTests : IDisposable
    {
        private const string AdminEmail = "admin@test.local";

        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        private WebApplicationFactory<Program> WithAdminConfig(string? email, string? password) =>
            _factory.WithWebHostBuilder(builder =>
            {
                if (email != null) builder.UseSetting("Admin:Email", email);
                if (password != null) builder.UseSetting("Admin:Password", password);
            });

        private Task<List<string?>> RolesOfAsync(string email) =>
            _factory.QueryAsync(db =>
                (from user in db.Users
                 join userRole in db.UserRoles on user.Id equals userRole.UserId
                 join role in db.Roles on userRole.RoleId equals role.Id
                 where user.Email == email
                 select role.Name).ToListAsync());

        [Fact]
        public async Task Startup_CreatesAdmin_WhoCanUseAdminEndpoints()
        {
            using var app = WithAdminConfig(AdminEmail, TuskApiFactory.Password);
            var client = app.CreateClient();

            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = AdminEmail, Password = TuskApiFactory.Password });
            login.EnsureSuccessStatusCode();
            var auth = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

            var response = await client.GetAsync("/api/admin/users-with-roles");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(["Admin"], await RolesOfAsync(AdminEmail));
        }

        [Fact]
        public async Task Startup_CreatesNoUsers_WhenAdminNotConfigured()
        {
            _ = _factory.Services;

            Assert.Equal(0, await _factory.QueryAsync(db => db.Users.CountAsync()));
        }

        [Fact]
        public async Task Startup_IsIdempotent_WhenAdminAlreadyExists()
        {
            using (var first = WithAdminConfig(AdminEmail, TuskApiFactory.Password)) _ = first.Services;
            using (var second = WithAdminConfig(AdminEmail, TuskApiFactory.Password)) _ = second.Services;

            Assert.Equal(1, await _factory.QueryAsync(db => db.Users.CountAsync()));
            Assert.Equal(["Admin"], await RolesOfAsync(AdminEmail));
        }

        [Fact]
        public async Task Startup_DoesNotPromote_ExistingNonAdminUser()
        {
            var customer = await _factory.AddUserAsync("Customer");

            using var app = WithAdminConfig(customer.Email, TuskApiFactory.Password);
            _ = app.Services;

            Assert.Equal(["Customer"], await RolesOfAsync(customer.Email!));
        }

        [Theory]
        [InlineData(AdminEmail, "weak")]
        [InlineData(AdminEmail, null)]
        [InlineData(null, TuskApiFactory.Password)]
        public async Task Startup_Fails_WhenAdminConfigIsInvalid(string? email, string? password)
        {
            using var app = WithAdminConfig(email, password);

            Assert.ThrowsAny<Exception>(() => app.Services);
            Assert.Equal(0, await _factory.QueryAsync(db => db.Users.CountAsync()));
        }
    }
}
