using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Tusk.Api.Data;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class SmokeTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        [Fact]
        public void App_UsesSqliteTestDatabase_NotConfiguredPostgres()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
        }

        [Fact]
        public async Task ProtectedEndpoint_Returns401_WithoutToken()
        {
            var response = await _factory.CreateClient().GetAsync("/api/projects");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ProtectedEndpoint_Returns200_WithTokenFromLogin()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await customer.Client.GetAsync("/api/projects");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
