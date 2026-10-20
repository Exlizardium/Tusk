using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tusk.Api.Data;
using Tusk.Api.Dtos.Projects;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    [Collection(PostgresCollection.Name)]
    public sealed class PostgresTests : IDisposable
    {
        private readonly PostgresApiFactory _factory;

        public PostgresTests(PostgresContainerFixture postgres)
        {
            _factory = new PostgresApiFactory(postgres);
        }

        public void Dispose() => _factory.Dispose();

        [Fact]
        public void App_UsesPerTestPostgresDatabase_NotConfiguredDevDatabase()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Equal(
                new NpgsqlConnectionStringBuilder(_factory.ConnectionString).Database,
                context.Database.GetDbConnection().Database);
        }

        [Fact]
        public async Task Migrations_ApplyCleanly_WithNoPendingMigrationsOrModelChanges()
        {
            using var context = _factory.CreateContext();

            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            Assert.Equal(context.Database.GetMigrations(), await context.Database.GetAppliedMigrationsAsync());
            Assert.False(context.Database.HasPendingModelChanges());
        }

        [Fact]
        public async Task CreateProject_Returns409_WhenNameAlreadyExists()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            var first = await admin.Client.PostAsJsonAsync("/api/projects", new CreateProjectDto { Name = "Alpha" });
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var response = await admin.Client.PostAsJsonAsync("/api/projects", new CreateProjectDto { Name = "Alpha" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(409, problem!.Status);
            Assert.Equal(1, await _factory.QueryAsync(db => db.Projects.CountAsync()));
        }

        [Fact]
        public async Task UpdateProject_Returns409_WhenRenamedToExistingName()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            await _factory.SeedProjectAsync(admin.Id, "Alpha");
            var beta = await _factory.SeedProjectAsync(admin.Id, "Beta");

            var response = await admin.Client.PutAsJsonAsync($"/api/projects/{beta.Id}", new UpdateProjectDto { Name = "Alpha" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var stored = await _factory.QueryAsync(db => db.Projects.SingleAsync(p => p.Id == beta.Id));
            Assert.Equal("Beta", stored.Name);
        }
    }
}
