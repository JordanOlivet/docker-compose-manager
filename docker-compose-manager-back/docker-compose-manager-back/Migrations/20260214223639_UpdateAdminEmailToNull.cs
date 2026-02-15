using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace docker_compose_manager_back.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminEmailToNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Email", "MustAddEmail" },
                values: new object[] { null, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Email", "MustAddEmail" },
                values: new object[] { "admin@localhost", false });
        }
    }
}
