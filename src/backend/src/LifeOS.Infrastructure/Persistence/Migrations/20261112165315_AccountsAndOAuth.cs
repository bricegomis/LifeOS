using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountsAndOAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR 0011: accounts move from Supabase to LifeOS. Households and everything they own
            // are reset (user decision, 2026-11-12); they are provisioned again at first sign-in.
            migrationBuilder.Sql("TRUNCATE TABLE households.households CASCADE;");

            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.RenameColumn(
                name: "SupabaseUserId",
                schema: "households",
                table: "household_members",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_household_members_SupabaseUserId",
                schema: "households",
                table: "household_members",
                newName: "IX_household_members_UserId");

            // Reviews reference this key: rename it instead of dropping it.
            migrationBuilder.Sql("ALTER TABLE households.household_members RENAME CONSTRAINT \"AK_household_members_SupabaseUserId_HouseholdId\" TO \"AK_household_members_UserId_HouseholdId\";");

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scopes",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSignInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "authorizations",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_authorizations_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "auth",
                        principalTable: "applications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tokens",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tokens_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "auth",
                        principalTable: "applications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tokens_authorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalSchema: "auth",
                        principalTable: "authorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_applications_ClientId",
                schema: "auth",
                table: "applications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_authorizations_ApplicationId_Status_Subject_Type",
                schema: "auth",
                table: "authorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_scopes_Name",
                schema: "auth",
                table: "scopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tokens_ApplicationId_Status_Subject_Type",
                schema: "auth",
                table: "tokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_tokens_AuthorizationId",
                schema: "auth",
                table: "tokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_tokens_ReferenceId",
                schema: "auth",
                table: "tokens",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_GoogleSubject",
                schema: "households",
                table: "users",
                column: "GoogleSubject",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_household_members_users_UserId",
                schema: "households",
                table: "household_members",
                column: "UserId",
                principalSchema: "households",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_household_members_users_UserId",
                schema: "households",
                table: "household_members");

            migrationBuilder.DropTable(
                name: "scopes",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "tokens",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "users",
                schema: "households");

            migrationBuilder.DropTable(
                name: "authorizations",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "applications",
                schema: "auth");

            migrationBuilder.RenameColumn(
                name: "UserId",
                schema: "households",
                table: "household_members",
                newName: "SupabaseUserId");

            migrationBuilder.RenameIndex(
                name: "IX_household_members_UserId",
                schema: "households",
                table: "household_members",
                newName: "IX_household_members_SupabaseUserId");

            migrationBuilder.Sql("ALTER TABLE households.household_members RENAME CONSTRAINT \"AK_household_members_UserId_HouseholdId\" TO \"AK_household_members_SupabaseUserId_HouseholdId\";");
        }
    }
}
