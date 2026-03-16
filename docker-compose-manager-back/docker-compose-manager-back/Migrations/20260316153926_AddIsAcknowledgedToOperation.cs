using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace docker_compose_manager_back.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAcknowledgedToOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAcknowledged",
                table: "Operations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAcknowledged",
                table: "Operations");
        }
    }
}
