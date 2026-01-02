using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuisinier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjouterKcal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kcal",
                table: "Recettes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecetteDetaillee",
                table: "Recettes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kcal",
                table: "Recettes");

            migrationBuilder.DropColumn(
                name: "RecetteDetaillee",
                table: "Recettes");
        }
    }
}
