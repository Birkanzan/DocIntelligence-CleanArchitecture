using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimizedSizeBytes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OptimizedSizeBytes",
                table: "Documents",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptimizedSizeBytes",
                table: "Documents");
        }
    }
}
