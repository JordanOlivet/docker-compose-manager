using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lighthouse.Migrations
{
    /// <summary>
    /// Data migration for the new PermissionFlags.Edit bit (256, compose file editing).
    /// Existing permissions that include Update (32) are considered "maintainers" of the
    /// resource and are granted Edit so the new feature does not silently disappear for
    /// users who previously had full-like access. Admins are unaffected (Full is computed
    /// at runtime, not stored).
    /// </summary>
    public partial class GrantEditPermissionWhereUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE ResourcePermissions SET Permissions = Permissions | 256 WHERE (Permissions & 32) != 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE ResourcePermissions SET Permissions = Permissions & ~256;");
        }
    }
}
