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
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Recipe configuration
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Ingredients)
                  .WithOne(e => e.Recipe)
                  .HasForeignKey(e => e.RecipeId)
                  .OnDelete(DeleteBehavior.Cascade);
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
    }
}

