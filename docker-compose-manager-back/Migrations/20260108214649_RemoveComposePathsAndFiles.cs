using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace docker_compose_manager_back.Migrations
{
    /// <inheritdoc />
    public partial class RemoveComposePathsAndFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WARNING: This migration drops the ComposePaths and ComposeFiles tables permanently
            // All data in these tables will be lost. This is intentional as the system is migrating
            // to a Docker-based discovery mechanism. See COMPOSE_DISCOVERY_SPECS.md for details.

            // Log existing paths before deletion (as SQL comment for reference)
            // SELECT Path FROM ComposePaths;

            // Drop ComposeFiles table first (has foreign key to ComposePaths)
            migrationBuilder.DropTable(
                name: "ComposeFiles");

            // Drop ComposePaths table
            migrationBuilder.DropTable(
                name: "ComposePaths");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate ComposePaths table
            migrationBuilder.CreateTable(
                name: "ComposePaths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComposePaths", x => x.Id);
                });

            // Recreate ComposeFiles table
            migrationBuilder.CreateTable(
                name: "ComposeFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ComposePathId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FullPath = table.Column<string>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastScanned = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDiscovered = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComposeFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComposeFiles_ComposePaths_ComposePathId",
                        column: x => x.ComposePathId,
                        principalTable: "ComposePaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Recreate indexes
            migrationBuilder.CreateIndex(
                name: "IX_ComposePaths_Path",
                table: "ComposePaths",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComposeFiles_ComposePathId",
                table: "ComposeFiles",
                column: "ComposePathId");

            migrationBuilder.CreateIndex(
                name: "IX_ComposeFiles_FullPath",
                table: "ComposeFiles",
                column: "FullPath",
                unique: true);
        }
    }
}
