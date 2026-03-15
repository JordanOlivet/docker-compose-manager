using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace docker_compose_manager_back.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerFieldsToOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear old operations that predate the action log feature —
            // they have no container fields and may be stuck in stale states.
            migrationBuilder.Sql("DELETE FROM Operations;");

            migrationBuilder.AddColumn<string>(
                name: "ContainerId",
                table: "Operations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContainerName",
                table: "Operations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContainerId",
                table: "Operations");

            migrationBuilder.DropColumn(
                name: "ContainerName",
                table: "Operations");
        }
    }
}
