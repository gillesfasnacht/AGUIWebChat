using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGUIWebChat.Server.Migrations
{
    /// <inheritdoc />
    public partial class SeedOllamaProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AIProviders",
                columns: new[] { "Id", "Endpoint", "IsEnabled", "Name", "ProviderType" },
                values: new object[] { 1, null, true, "Ollama", "Ollama" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AIProviders",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
