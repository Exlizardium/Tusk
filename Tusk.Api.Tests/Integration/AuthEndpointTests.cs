using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Auth;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class AuthEndpointTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();
        private readonly HttpClient _client;

        public AuthEndpointTests()
        {
            _client = _factory.CreateClient();
        }

        public void Dispose() => _factory.Dispose();

        private static RegisterDto NewRegistration(string email = "jane@test.local", string password = TuskApiFactory.Password) =>
            new() { FirstName = "Jane", LastName = "Doe", Email = email, Password = password };

        private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
            _client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = password });

        [Fact]
        public async Task Register_CreatesUser_WithOnlyCustomerRole()
        {
            var response = await _client.PostAsJsonAsync("/api/auth/register", NewRegistration());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var roles = await _factory.QueryAsync(db =>
                (from user in db.Users
                 join userRole in db.UserRoles on user.Id equals userRole.UserId
                 join role in db.Roles on userRole.RoleId equals role.Id
                 where user.Email == "jane@test.local"
                 select role.Name).ToListAsync());
            Assert.Equal(["Customer"], roles);
        }

        [Fact]
        public async Task Register_Returns400_WhenEmailAlreadyRegistered()
        {
            await _client.PostAsJsonAsync("/api/auth/register", NewRegistration());

            var response = await _client.PostAsJsonAsync("/api/auth/register", NewRegistration());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(1, await _factory.QueryAsync(db => db.Users.CountAsync()));
        }

        [Fact]
        public async Task Register_Returns400_WhenPasswordIsTooWeak()
        {
            var response = await _client.PostAsJsonAsync("/api/auth/register", NewRegistration(password: "weak"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Users.AnyAsync()));
        }

        [Fact]
        public async Task Login_AfterRegister_IssuesTokenWithUserIdAndCustomerRole()
        {
            await _client.PostAsJsonAsync("/api/auth/register", NewRegistration());
            var userId = await _factory.QueryAsync(db => db.Users.Select(u => u.Id).SingleAsync());

            var response = await LoginAsync("jane@test.local", TuskApiFactory.Password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
            var token = new JwtSecurityTokenHandler().ReadJwtToken(auth!.Token);
            Assert.Equal(userId, token.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
            Assert.Equal("Customer", token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        }

        [Fact]
        public async Task Login_Returns401_ForUnknownEmail()
        {
            var response = await LoginAsync("nobody@test.local", TuskApiFactory.Password);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_Returns401_ForWrongPassword()
        {
            var user = await _factory.AddUserAsync("Customer");

            var response = await LoginAsync(user.Email!, "Wr0ng-password!");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_LocksAccountAfterFiveFailures_AndThenRejectsCorrectPassword()
        {
            var user = await _factory.AddUserAsync("Customer");

            for (var attempt = 1; attempt <= 4; attempt++)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(user.Email!, "Wr0ng-password!")).StatusCode);
            }

            Assert.Equal(HttpStatusCode.Locked, (await LoginAsync(user.Email!, "Wr0ng-password!")).StatusCode);
            Assert.Equal(HttpStatusCode.Locked, (await LoginAsync(user.Email!, TuskApiFactory.Password)).StatusCode);
        }

        [Fact]
        public async Task Login_SuccessResetsFailureCount()
        {
            var user = await _factory.AddUserAsync("Customer");

            for (var attempt = 1; attempt <= 4; attempt++)
            {
                await LoginAsync(user.Email!, "Wr0ng-password!");
            }
            Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Email!, TuskApiFactory.Password)).StatusCode);

            for (var attempt = 1; attempt <= 4; attempt++)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(user.Email!, "Wr0ng-password!")).StatusCode);
            }
        }
    }
}
