using AGUIWebChat.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Tests.Infrastructure
{
    public sealed class TestDbContextFactory : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDbContextFactory(SqliteConnection connection, ChatDbContext dbContext)
        {
            _connection = connection;
            DbContext = dbContext;
        }

        public ChatDbContext DbContext { get; }

        public static async Task<TestDbContextFactory> CreateAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync(cancellationToken);

            var options = new DbContextOptionsBuilder<ChatDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new ChatDbContext(options);

            await dbContext.Database.EnsureCreatedAsync(cancellationToken);

            return new TestDbContextFactory(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
