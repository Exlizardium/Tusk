using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;
using Tusk.Api.Models;

namespace Tusk.Api.Tests.Infrastructure
{
    public sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public AppDbContext CreateContext() => new(_options);

        public async Task<ApplicationUser> AddUserAsync(string firstName = "Test", string lastName = "User")
        {
            var user = new ApplicationUser
            {
                UserName = $"{firstName}.{lastName}.{Guid.NewGuid():N}",
                FirstName = firstName,
                LastName = lastName
            };

            await using var context = CreateContext();
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        public void Dispose() => _connection.Dispose();
    }
}
