using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;

namespace Tusk.Api.Tests.Infrastructure
{
    public sealed class PostgresApiFactory : ApiFactoryBase
    {
        public string ConnectionString { get; }

        public PostgresApiFactory(PostgresContainerFixture postgres)
        {
            ConnectionString = postgres.CreateDatabaseConnectionString();

            using var context = CreateContext();
            context.Database.Migrate();
        }

        public AppDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

        protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
            options.UseNpgsql(ConnectionString);
    }
}
