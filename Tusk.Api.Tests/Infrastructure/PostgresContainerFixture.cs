using Npgsql;
using Testcontainers.PostgreSql;

namespace Tusk.Api.Tests.Infrastructure
{
    public sealed class PostgresContainerFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        public string CreateDatabaseConnectionString() =>
            new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
            {
                Database = $"tusk_test_{Guid.NewGuid():N}"
            }.ConnectionString;
    }

    [CollectionDefinition(Name)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
    {
        public const string Name = "Postgres";
    }
}
