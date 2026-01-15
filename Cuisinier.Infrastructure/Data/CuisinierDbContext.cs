using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Cuisinier.Core.Entities;

namespace Cuisinier.Infrastructure.Data;

public class CuisinierDbContext : IdentityDbContext<ApplicationUser>
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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FamilyLink> FamilyLinks => Set<FamilyLink>();
    public DbSet<FamilyLinkInvitation> FamilyLinkInvitations => Set<FamilyLinkInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Menu configuration
        modelBuilder.Entity<Menu>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WeekStartDate);
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User)
                  .WithMany(e => e.Menus)
                  .HasForeignKey(e => e.UserId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.NoAction);
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
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User)
                  .WithMany(e => e.ShoppingLists)
                  .HasForeignKey(e => e.UserId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Menu)
                  .WithMany()
                  .HasForeignKey(e => e.MenuId)
                  .OnDelete(DeleteBehavior.NoAction);
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
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User)
                  .WithOne(e => e.MenuSettings)
                  .HasForeignKey<MenuSettings>(e => e.UserId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Favorite configuration
        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User)
                  .WithMany(e => e.Favorites)
                  .HasForeignKey(e => e.UserId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User)
                  .WithMany(e => e.Dishes)
                  .HasForeignKey(e => e.UserId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
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

        // RefreshToken configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Token);
            entity.HasOne(e => e.User)
                  .WithMany(e => e.RefreshTokens)
                  .HasForeignKey(e => e.UserId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // FamilyLink configuration
        modelBuilder.Entity<FamilyLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.User1Id);
            entity.HasIndex(e => e.User2Id);
            entity.HasIndex(e => new { e.User1Id, e.User2Id }).IsUnique();
            entity.HasOne(e => e.User1)
                  .WithMany(e => e.FamilyLinksAsUser1)
                  .HasForeignKey(e => e.User1Id)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.User2)
                  .WithMany(e => e.FamilyLinksAsUser2)
                  .HasForeignKey(e => e.User2Id)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // FamilyLinkInvitation configuration
        modelBuilder.Entity<FamilyLinkInvitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token);
            entity.HasIndex(e => e.InviterUserId);
            entity.HasIndex(e => e.InvitedEmail);
            entity.HasOne(e => e.InviterUser)
                  .WithMany(e => e.SentInvitations)
                  .HasForeignKey(e => e.InviterUserId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.InvitedUser)
                  .WithMany(e => e.ReceivedInvitations)
                  .HasForeignKey(e => e.InvitedUserId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.NoAction);
        });
    }
}

