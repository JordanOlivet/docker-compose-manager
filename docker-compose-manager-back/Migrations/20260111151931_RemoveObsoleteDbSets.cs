using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace docker_compose_manager_back.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteDbSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op migration: ComposePaths and ComposeFiles tables were already removed
            // by migration RemoveComposePathsAndFiles (20260108214649).
            // This migration only updates the EF Core model snapshot to reflect that
            // the DbSets have been removed from AppDbContext.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComposePaths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComposePaths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComposeFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ComposePathId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FullPath = table.Column<string>(type: "TEXT", nullable: false),
                    IsDiscovered = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastScanned = table.Column<DateTime>(type: "TEXT", nullable: false)
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

            migrationBuilder.InsertData(
                table: "ComposePaths",
                columns: new[] { "Id", "CreatedAt", "IsEnabled", "IsReadOnly", "Path", "UpdatedAt" },
                values: new object[] { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "/compose-files", null });

            migrationBuilder.CreateIndex(
                name: "IX_ComposeFiles_ComposePathId",
                table: "ComposeFiles",
                column: "ComposePathId");

            migrationBuilder.CreateIndex(
                name: "IX_ComposeFiles_FullPath",
                table: "ComposeFiles",
                column: "FullPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComposePaths_Path",
                table: "ComposePaths",
                column: "Path",
                unique: true);
        }
    }
}
