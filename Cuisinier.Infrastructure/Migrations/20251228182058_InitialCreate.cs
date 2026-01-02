using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuisinier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Menus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DateDebutSemaine = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ListeCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuId = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListeCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListeCourses_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recettes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuId = table.Column<int>(type: "int", nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionComplete = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TempsPreparation = table.Column<TimeSpan>(type: "time", nullable: true),
                    TempsCuisson = table.Column<TimeSpan>(type: "time", nullable: true),
                    Personnes = table.Column<int>(type: "int", nullable: false),
                    EstDeLaBaseDeDonnees = table.Column<bool>(type: "bit", nullable: false),
                    PlatOriginalId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recettes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recettes_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recettes_Recettes_PlatOriginalId",
                        column: x => x.PlatOriginalId,
                        principalTable: "Recettes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItemListeCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListeCourseId = table.Column<int>(type: "int", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Categorie = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstAjouteManuellement = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemListeCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemListeCourses_ListeCourses_ListeCourseId",
                        column: x => x.ListeCourseId,
                        principalTable: "ListeCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientRecettes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecetteId = table.Column<int>(type: "int", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Categorie = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRecettes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientRecettes_Recettes_RecetteId",
                        column: x => x.RecetteId,
                        principalTable: "Recettes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRecettes_RecetteId",
                table: "IngredientRecettes",
                column: "RecetteId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemListeCourses_ListeCourseId",
                table: "ItemListeCourses",
                column: "ListeCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ListeCourses_MenuId",
                table: "ListeCourses",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_DateDebutSemaine",
                table: "Menus",
                column: "DateDebutSemaine");

            migrationBuilder.CreateIndex(
                name: "IX_Recettes_MenuId",
                table: "Recettes",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_Recettes_PlatOriginalId",
                table: "Recettes",
                column: "PlatOriginalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientRecettes");

            migrationBuilder.DropTable(
                name: "ItemListeCourses");

            migrationBuilder.DropTable(
                name: "Recettes");

            migrationBuilder.DropTable(
                name: "ListeCourses");

            migrationBuilder.DropTable(
                name: "Menus");
        }
    }
}
