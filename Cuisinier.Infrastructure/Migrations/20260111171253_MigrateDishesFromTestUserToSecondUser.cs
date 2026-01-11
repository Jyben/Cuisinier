using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuisinier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateDishesFromTestUserToSecondUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate data from test user (migration@cuisinier.local) to the second user
            // Migrates: Dishes, Menus, and ShoppingLists
            // Only execute if the second user exists
            migrationBuilder.Sql(@"
                DECLARE @TestUserEmail NVARCHAR(256) = 'migration@cuisinier.local';
                DECLARE @TestUserId NVARCHAR(450) = NULL;
                DECLARE @SecondUserId NVARCHAR(450) = NULL;
                DECLARE @DishesCount INT = 0;
                DECLARE @MenusCount INT = 0;
                DECLARE @ShoppingListsCount INT = 0;

                -- Find the test user
                SELECT @TestUserId = Id
                FROM AspNetUsers
                WHERE NormalizedEmail = UPPER(@TestUserEmail);

                -- Find the second user (the one that is NOT the test user)
                -- Order by CreatedAt to get the second user (after the test user)
                SELECT TOP 1 @SecondUserId = Id
                FROM AspNetUsers
                WHERE Id != @TestUserId
                ORDER BY CreatedAt ASC;

                -- Only proceed if both users exist
                IF @TestUserId IS NOT NULL AND @SecondUserId IS NOT NULL
                BEGIN
                    -- Count items to migrate
                    SELECT @DishesCount = COUNT(*)
                    FROM Dishes
                    WHERE UserId = @TestUserId;

                    SELECT @MenusCount = COUNT(*)
                    FROM Menus
                    WHERE UserId = @TestUserId;

                    SELECT @ShoppingListsCount = COUNT(*)
                    FROM ShoppingLists
                    WHERE UserId = @TestUserId;

                    -- Migrate dishes from test user to second user
                    IF @DishesCount > 0
                    BEGIN
                        UPDATE Dishes
                        SET UserId = @SecondUserId
                        WHERE UserId = @TestUserId;

                        PRINT 'Migrated ' + CAST(@DishesCount AS NVARCHAR(10)) + ' dishes from test user to second user.';
                    END

                    -- Migrate menus from test user to second user
                    IF @MenusCount > 0
                    BEGIN
                        UPDATE Menus
                        SET UserId = @SecondUserId
                        WHERE UserId = @TestUserId;

                        PRINT 'Migrated ' + CAST(@MenusCount AS NVARCHAR(10)) + ' menus from test user to second user.';
                    END

                    -- Migrate shopping lists from test user to second user
                    IF @ShoppingListsCount > 0
                    BEGIN
                        UPDATE ShoppingLists
                        SET UserId = @SecondUserId
                        WHERE UserId = @TestUserId;

                        PRINT 'Migrated ' + CAST(@ShoppingListsCount AS NVARCHAR(10)) + ' shopping lists from test user to second user.';
                    END

                    IF @DishesCount = 0 AND @MenusCount = 0 AND @ShoppingListsCount = 0
                    BEGIN
                        PRINT 'No data found to migrate from test user.';
                    END
                END
                ELSE
                BEGIN
                    IF @TestUserId IS NULL
                        PRINT 'Test user (migration@cuisinier.local) not found. Migration skipped.';
                    IF @SecondUserId IS NULL
                        PRINT 'Second user not found. Migration skipped.';
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse migration: move dishes back to test user
            // This is optional and may not be needed, but included for completeness
            migrationBuilder.Sql(@"
                DECLARE @TestUserEmail NVARCHAR(256) = 'migration@cuisinier.local';
                DECLARE @TestUserId NVARCHAR(450) = NULL;
                DECLARE @SecondUserId NVARCHAR(450) = NULL;

                -- Find the test user
                SELECT @TestUserId = Id
                FROM AspNetUsers
                WHERE NormalizedEmail = UPPER(@TestUserEmail);

                -- Find the second user
                SELECT TOP 1 @SecondUserId = Id
                FROM AspNetUsers
                WHERE Id != @TestUserId
                ORDER BY CreatedAt ASC;

                -- Reverse migration (optional - may not be accurate)
                IF @TestUserId IS NOT NULL AND @SecondUserId IS NOT NULL
                BEGIN
                    -- Note: This is a best-effort reversal and may not be 100% accurate
                    -- as we cannot determine which dishes originally belonged to the test user
                    PRINT 'Reverse migration: This operation cannot be accurately reversed.';
                END
            ");
        }
    }
}
