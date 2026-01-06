using Microsoft.EntityFrameworkCore;
using Cuisinier.Core.Entities;

namespace Cuisinier.Infrastructure.Data;

public class CuisinierDbContext : DbContext
{
    public CuisinierDbContext(DbContextOptions<CuisinierDbContext> options) : base(options)
    {
    }

    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<MenuSettings> MenuSettings => Set<MenuSettings>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<FavoriteIngredient> FavoriteIngredients => Set<FavoriteIngredient>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<DishIngredient> DishIngredients => Set<DishIngredient>();
    public DbSet<ShoppingListDish> ShoppingListDishes => Set<ShoppingListDish>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Menu configuration
        modelBuilder.Entity<Menu>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WeekStartDate);
            entity.HasMany(e => e.Recipes)
                  .WithOne(e => e.Menu)
                  .HasForeignKey(e => e.MenuId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Recipe configuration
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Ingredients)
                  .WithOne(e => e.Recipe)
                  .HasForeignKey(e => e.RecipeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Dish)
                  .WithMany(e => e.Recipes)
                  .HasForeignKey(e => e.DishId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.OriginalDish)
                  .WithMany()
                  .HasForeignKey(e => e.OriginalDishId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // RecipeIngredient configuration
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // ShoppingList configuration
        modelBuilder.Entity<ShoppingList>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Menu)
                  .WithMany()
                  .HasForeignKey(e => e.MenuId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.ShoppingList)
                  .HasForeignKey(e => e.ShoppingListId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ShoppingListDishes)
                  .WithOne(e => e.ShoppingList)
                  .HasForeignKey(e => e.ShoppingListId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ShoppingListItem configuration
        modelBuilder.Entity<ShoppingListItem>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // MenuSettings configuration
        modelBuilder.Entity<MenuSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Disable IDENTITY to allow explicit insertion of Id = 1
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        // Favorite configuration
        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Title);
            entity.HasMany(e => e.Ingredients)
                  .WithOne(e => e.Favorite)
                  .HasForeignKey(e => e.FavoriteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // FavoriteIngredient configuration
        modelBuilder.Entity<FavoriteIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // Dish configuration
        modelBuilder.Entity<Dish>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Title);
            entity.HasMany(e => e.Ingredients)
                  .WithOne(e => e.Dish)
                  .HasForeignKey(e => e.DishId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // DishIngredient configuration
        modelBuilder.Entity<DishIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // ShoppingListDish configuration (many-to-many)
        modelBuilder.Entity<ShoppingListDish>(entity =>
        {
            entity.HasKey(e => new { e.ShoppingListId, e.DishId });
            entity.HasOne(e => e.ShoppingList)
                  .WithMany(e => e.ShoppingListDishes)
                  .HasForeignKey(e => e.ShoppingListId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Dish)
                  .WithMany(e => e.ShoppingListDishes)
                  .HasForeignKey(e => e.DishId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

