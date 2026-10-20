using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;

namespace Tusk.Api.Tests.Infrastructure
{
    public sealed class TuskApiFactory : ApiFactoryBase
    {
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

        protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
            options.UseSqlite(_connection);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _connection.Dispose();
        }
    }
}
