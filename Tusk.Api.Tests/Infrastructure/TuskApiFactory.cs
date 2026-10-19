using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tusk.Api.Data;
using Tusk.Api.Dtos.Auth;
using Tusk.Api.Models;

namespace Tusk.Api.Tests.Infrastructure
{
    public sealed record TestUser(string Id, string Email, HttpClient Client);

    public sealed class TuskApiFactory : WebApplicationFactory<Program>
    {
        public const string Password = "Passw0rd!";

        private readonly SqliteConnection _connection;

        public TuskApiFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AppDbContext(options);
            context.Database.EnsureCreated();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:Key", "integration-tests-only-signing-key-0123456789abcdefghijklmnopqrstuvwxyz");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            });
        }

        public async Task<T> SeedAsync<T>(T entity) where T : class
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Add(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task<TResult> QueryAsync<TResult>(Func<AppDbContext, Task<TResult>> query)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await query(context);
        }

        public async Task<ApplicationUser> AddUserAsync(string role)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = role,
                LastName = "Test"
            };

            EnsureSucceeded(await userManager.CreateAsync(user, Password));
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
            return user;
        }

        public async Task<TestUser> LoginAsNewUserAsync(string role)
        {
            var user = await AddUserAsync(role);
            var client = CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = user.Email!, Password = Password });
            response.EnsureSuccessStatusCode();
            var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
            return new TestUser(user.Id, user.Email!, client);
        }

        private static void EnsureSucceeded(IdentityResult result)
        {
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _connection.Dispose();
        }
    }
}
