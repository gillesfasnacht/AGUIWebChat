using AGUIWebChat.Server;
using AGUIWebChat.Server.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace AGUIWebChat.Tests.Infrastructure
{
    public sealed class AGUIWebChatWebApplicationFactory : WebApplicationFactory<AGUIWebChatServerMarker>
    {
        private SqliteConnection? _connection;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Catalogue tests never call inference; do not inherit a developer's Ollama endpoint.
                    ["OLLAMA_ENDPOINT"] = "http://127.0.0.1:1",
                    ["OLLAMA_MODEL"] = "catalog-test-model"
                }));

            builder.ConfigureServices(services =>
            {
                _connection = new SqliteConnection("Data Source=:memory:");

                _connection.Open();

                services.AddDbContext<ChatDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                using var scope = services.BuildServiceProvider().CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

                dbContext.Database.EnsureCreated();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _connection?.Dispose();
            }
        }
    }
}
