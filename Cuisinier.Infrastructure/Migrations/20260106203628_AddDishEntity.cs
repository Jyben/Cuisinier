using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuisinier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDishEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Dishes table
            migrationBuilder.CreateTable(
                name: "Dishes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompleteDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetailedRecipe = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreparationTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CookingTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Kcal = table.Column<int>(type: "int", nullable: true),
                    Servings = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dishes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dishes_Title",
                table: "Dishes",
                column: "Title");

            // Create DishIngredients table
            migrationBuilder.CreateTable(
                name: "DishIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DishId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DishIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DishIngredients_Dishes_DishId",
                        column: x => x.DishId,
                        principalTable: "Dishes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DishIngredients_DishId",
                table: "DishIngredients",
                column: "DishId");

            // Create ShoppingListDishes table (many-to-many)
            migrationBuilder.CreateTable(
                name: "ShoppingListDishes",
                columns: table => new
                {
                    ShoppingListId = table.Column<int>(type: "int", nullable: false),
                    DishId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListDishes", x => new { x.ShoppingListId, x.DishId });
                    table.ForeignKey(
                        name: "FK_ShoppingListDishes_ShoppingLists_ShoppingListId",
                        column: x => x.ShoppingListId,
                        principalTable: "ShoppingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListDishes_Dishes_DishId",
                        column: x => x.DishId,
                        principalTable: "Dishes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListDishes_DishId",
                table: "ShoppingListDishes",
                column: "DishId");

            // Modify Recipes table: make MenuId nullable and add DishId
            migrationBuilder.AlterColumn<int>(
                name: "MenuId",
                table: "Recipes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DishId",
                table: "Recipes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_DishId",
                table: "Recipes",
                column: "DishId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Dishes_DishId",
                table: "Recipes",
                column: "DishId",
                principalTable: "Dishes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Update OriginalDishId foreign key to reference Dishes instead of Recipes
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Recipes_OriginalDishId",
                table: "Recipes");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Dishes_OriginalDishId",
                table: "Recipes",
                column: "OriginalDishId",
                principalTable: "Dishes",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore OriginalDishId foreign key to Recipes
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Dishes_OriginalDishId",
                table: "Recipes");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Recipes_OriginalDishId",
                table: "Recipes",
                column: "OriginalDishId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            // Remove DishId from Recipes
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Dishes_DishId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_DishId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "DishId",
                table: "Recipes");

            migrationBuilder.AlterColumn<int>(
                name: "MenuId",
                table: "Recipes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Drop ShoppingListDishes table
            migrationBuilder.DropTable(
                name: "ShoppingListDishes");

            // Drop DishIngredients table
            migrationBuilder.DropTable(
                name: "DishIngredients");

            // Drop Dishes table
            migrationBuilder.DropTable(
                name: "Dishes");
        }
    }
}
