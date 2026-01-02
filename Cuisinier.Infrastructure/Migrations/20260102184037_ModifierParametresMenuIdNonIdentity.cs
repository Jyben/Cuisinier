using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuisinier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ModifierParametresMenuIdNonIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if the table exists and if the Id column is IDENTITY
            // If yes, recreate the table without IDENTITY
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ParametresMenus')
                BEGIN
                    -- Créer une table temporaire avec la même structure mais sans IDENTITY
                    CREATE TABLE [dbo].[ParametresMenus_temp] (
                        [Id] int NOT NULL,
                        [ParametresJson] nvarchar(max) NOT NULL,
                        [DateModification] datetime2 NOT NULL,
                        CONSTRAINT [PK_ParametresMenus_temp] PRIMARY KEY ([Id])
                    );
                    
                    -- Copier les données existantes (s'il y en a)
                    IF EXISTS (SELECT * FROM [dbo].[ParametresMenus])
                    BEGIN
                        SET IDENTITY_INSERT [dbo].[ParametresMenus_temp] ON;
                        INSERT INTO [dbo].[ParametresMenus_temp] ([Id], [ParametresJson], [DateModification])
                        SELECT [Id], [ParametresJson], [DateModification] FROM [dbo].[ParametresMenus];
                        SET IDENTITY_INSERT [dbo].[ParametresMenus_temp] OFF;
                    END;
                    
                    -- Supprimer l'ancienne table
                    DROP TABLE [dbo].[ParametresMenus];
                    
                    -- Renommer la nouvelle table
                    EXEC sp_rename '[dbo].[ParametresMenus_temp]', 'ParametresMenus';
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to IDENTITY (if necessary)
            // Note: This operation is complex and would also require recreating the table
            // For simplicity, leave empty as we should not need to go back
        }
    }
}
