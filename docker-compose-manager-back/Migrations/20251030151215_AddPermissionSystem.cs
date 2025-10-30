using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace docker_compose_manager_back.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourcePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserGroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    Permissions = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourcePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_UserGroups_UserGroupId",
                        column: x => x.UserGroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserGroupMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroupMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGroupMemberships_UserGroups_UserGroupId",
                        column: x => x.UserGroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGroupMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_ResourceType_ResourceName",
                table: "ResourcePermissions",
                columns: new[] { "ResourceType", "ResourceName" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_UserGroupId",
                table: "ResourcePermissions",
                column: "UserGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_UserId",
                table: "ResourcePermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMemberships_UserGroupId",
                table: "UserGroupMemberships",
                column: "UserGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMemberships_UserId_UserGroupId",
                table: "UserGroupMemberships",
                columns: new[] { "UserId", "UserGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_Name",
                table: "UserGroups",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourcePermissions");

            migrationBuilder.DropTable(
                name: "UserGroupMemberships");

            migrationBuilder.DropTable(
                name: "UserGroups");
        }
    }
}
