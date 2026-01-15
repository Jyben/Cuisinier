using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuisinier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyLinkInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InviterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InvitedEmail = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InvitedUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Token = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyLinkInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyLinkInvitations_AspNetUsers_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FamilyLinkInvitations_AspNetUsers_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FamilyLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    User1Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    User2Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyLinks_AspNetUsers_User1Id",
                        column: x => x.User1Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FamilyLinks_AspNetUsers_User2Id",
                        column: x => x.User2Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyLinkInvitations_InvitedEmail",
                table: "FamilyLinkInvitations",
                column: "InvitedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyLinkInvitations_InvitedUserId",
                table: "FamilyLinkInvitations",
                column: "InvitedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyLinkInvitations_InviterUserId",
                table: "FamilyLinkInvitations",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyLinkInvitations_Token",
                table: "FamilyLinkInvitations",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyLinks_User1Id",
                table: "FamilyLinks",
                column: "User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyLinks_User1Id_User2Id",
                table: "FamilyLinks",
                columns: new[] { "User1Id", "User2Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyLinks_User2Id",
                table: "FamilyLinks",
                column: "User2Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyLinkInvitations");

            migrationBuilder.DropTable(
                name: "FamilyLinks");
        }
    }
}
