using AGUIWebChat.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace AGUIWebChat.Server.Tests.Services.Chat;

public sealed class ConversationMigrationTests
{
    [Fact]
    public void SqlServerMigration_ShouldMatchModelAndGenerateAdditiveSql()
    {
        using var db = new ChatDbContext(new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlServer("Server=localhost;Database=MigrationScript;Integrated Security=true").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var sql = db.GetService<IMigrator>().GenerateScript("20261003201805_AddAIAgents",
            options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [ChatConversations]", sql);
        Assert.Contains("CREATE TABLE [ChatMessages]", sql);
        Assert.Contains("ON DELETE CASCADE", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
    }
}

