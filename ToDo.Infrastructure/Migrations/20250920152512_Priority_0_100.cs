using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToDo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Priority_0_100 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TodoItems_Priority_Range",
                table: "TodoItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TodoItem_Priority_0_100",
                table: "TodoItems",
                sql: "\"Priority\" BETWEEN 0 AND 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TodoItem_Priority_0_100",
                table: "TodoItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TodoItems_Priority_Range",
                table: "TodoItems",
                sql: "\"Priority\" >= 0 AND \"Priority\" <= 100");
        }
    }
}
