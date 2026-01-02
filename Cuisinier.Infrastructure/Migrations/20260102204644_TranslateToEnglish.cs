using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuisinier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TranslateToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientRecettes");

            migrationBuilder.DropTable(
                name: "ItemListeCourses");

            migrationBuilder.DropTable(
                name: "ParametresMenus");

            migrationBuilder.DropTable(
                name: "Recettes");

            migrationBuilder.DropTable(
                name: "ListeCourses");

            migrationBuilder.RenameColumn(
                name: "ParametresGenerationJson",
                table: "Menus",
                newName: "GenerationParametersJson");

            migrationBuilder.RenameColumn(
                name: "DateDebutSemaine",
                table: "Menus",
                newName: "WeekStartDate");

            migrationBuilder.RenameColumn(
                name: "DateCreation",
                table: "Menus",
                newName: "CreationDate");

            migrationBuilder.RenameIndex(
                name: "IX_Menus_DateDebutSemaine",
                table: "Menus",
                newName: "IX_Menus_WeekStartDate");

            migrationBuilder.CreateTable(
                name: "MenuSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompleteDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetailedRecipe = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreparationTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CookingTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Kcal = table.Column<int>(type: "int", nullable: true),
                    Servings = table.Column<int>(type: "int", nullable: false),
                    IsFromDatabase = table.Column<bool>(type: "bit", nullable: false),
                    OriginalDishId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipes_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipes_Recipes_OriginalDishId",
                        column: x => x.OriginalDishId,
                        principalTable: "Recipes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ShoppingLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuId = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingLists_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShoppingListId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsManuallyAdded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListItems_ShoppingLists_ShoppingListId",
                        column: x => x.ShoppingListId,
                        principalTable: "ShoppingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId",
                table: "RecipeIngredients",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_MenuId",
                table: "Recipes",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OriginalDishId",
                table: "Recipes",
                column: "OriginalDishId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_ShoppingListId",
                table: "ShoppingListItems",
                column: "ShoppingListId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_MenuId",
                table: "ShoppingLists",
                column: "MenuId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuSettings");

            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "ShoppingListItems");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "ShoppingLists");

            migrationBuilder.RenameColumn(
                name: "WeekStartDate",
                table: "Menus",
                newName: "DateDebutSemaine");

            migrationBuilder.RenameColumn(
                name: "GenerationParametersJson",
                table: "Menus",
                newName: "ParametresGenerationJson");

            migrationBuilder.RenameColumn(
                name: "CreationDate",
                table: "Menus",
                newName: "DateCreation");

            migrationBuilder.RenameIndex(
                name: "IX_Menus_WeekStartDate",
                table: "Menus",
                newName: "IX_Menus_DateDebutSemaine");

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
                name: "ParametresMenus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ParametresJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametresMenus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recettes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuId = table.Column<int>(type: "int", nullable: false),
                    PlatOriginalId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionComplete = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstDeLaBaseDeDonnees = table.Column<bool>(type: "bit", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Kcal = table.Column<int>(type: "int", nullable: true),
                    Personnes = table.Column<int>(type: "int", nullable: false),
                    RecetteDetaillee = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TempsCuisson = table.Column<TimeSpan>(type: "time", nullable: true),
                    TempsPreparation = table.Column<TimeSpan>(type: "time", nullable: true),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                    Categorie = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstAjouteManuellement = table.Column<bool>(type: "bit", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantite = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                    Categorie = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantite = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                name: "IX_Recettes_MenuId",
                table: "Recettes",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_Recettes_PlatOriginalId",
                table: "Recettes",
                column: "PlatOriginalId");
        }
    }
}
