using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuisinier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToMenuSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete existing MenuSettings record (Id = 1) as we're not migrating data
            migrationBuilder.Sql("DELETE FROM MenuSettings WHERE Id = 1");

            // Drop the primary key constraint first
            migrationBuilder.DropPrimaryKey(
                name: "PK_MenuSettings",
                table: "MenuSettings");

            // Drop and recreate the Id column with IDENTITY
            migrationBuilder.DropColumn(
                name: "Id",
                table: "MenuSettings");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "MenuSettings",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            // Recreate the primary key
            migrationBuilder.AddPrimaryKey(
                name: "PK_MenuSettings",
                table: "MenuSettings",
                column: "Id");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "MenuSettings",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSettings_UserId",
                table: "MenuSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuSettings_AspNetUsers_UserId",
                table: "MenuSettings",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuSettings_AspNetUsers_UserId",
                table: "MenuSettings");

            migrationBuilder.DropIndex(
                name: "IX_MenuSettings_UserId",
                table: "MenuSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MenuSettings",
                table: "MenuSettings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MenuSettings");

            // Drop and recreate the Id column without IDENTITY
            migrationBuilder.DropColumn(
                name: "Id",
                table: "MenuSettings");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "MenuSettings",
                type: "int",
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MenuSettings",
                table: "MenuSettings",
                column: "Id");
        }
    }
}
