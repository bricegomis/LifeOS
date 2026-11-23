using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BudgetModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "budget");

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "budget",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Account = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    BalanceOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LastImportAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    LastTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_accounts_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lines",
                schema: "budget",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Reduce = table.Column<bool>(type: "boolean", nullable: false),
                    FromSupplements = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StoppedFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lines", x => x.Id);
                    table.UniqueConstraint("AK_lines_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
                    table.CheckConstraint("ck_budget_line_kind", "\"Kind\" IN ('fixed', 'envelope', 'extra', 'off', 'income')");
                    table.CheckConstraint("ck_budget_line_supplements", "NOT \"FromSupplements\" OR \"Kind\" IN ('fixed', 'envelope')");
                    table.ForeignKey(
                        name: "FK_lines_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lines_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lines_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "line_amounts",
                schema: "budget",
                columns: table => new
                {
                    FromMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Months = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_line_amounts", x => new { x.LineId, x.FromMonth });
                    table.CheckConstraint("ck_budget_amount_period", "\"Period\" IN ('month', 'year', 'months')");
                    table.CheckConstraint("ck_budget_amount_values", "\"Amount\" >= 0 AND \"Months\" >= 1 AND \"Months\" <= 60");
                    table.ForeignKey(
                        name: "FK_line_amounts_lines_LineId",
                        column: x => x.LineId,
                        principalSchema: "budget",
                        principalTable: "lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rules",
                schema: "budget",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Spread = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rules", x => x.Id);
                    table.CheckConstraint("ck_budget_rule_direction", "\"Direction\" IN ('any', 'out', 'in')");
                    table.CheckConstraint("ck_budget_rule_spread", "\"Spread\" >= 1 AND \"Spread\" <= 36");
                    table.ForeignKey(
                        name: "FK_rules_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rules_lines_LineId_OwnerUserId",
                        columns: x => new { x.LineId, x.OwnerUserId },
                        principalSchema: "budget",
                        principalTable: "lines",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rules_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rules_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operations",
                schema: "budget",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Account = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BankId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PostedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Spread = table.Column<int>(type: "integer", nullable: false),
                    ClassifiedBy = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations", x => x.Id);
                    table.CheckConstraint("ck_budget_operation_bank", "(\"Source\" = 'ofx') = (\"BankId\" IS NOT NULL AND \"Account\" IS NOT NULL)");
                    table.CheckConstraint("ck_budget_operation_by", "\"ClassifiedBy\" IS NULL OR \"ClassifiedBy\" IN ('rule', 'hand', 'assistant')");
                    table.CheckConstraint("ck_budget_operation_classified", "(\"LineId\" IS NULL) = (\"ClassifiedBy\" IS NULL)");
                    table.CheckConstraint("ck_budget_operation_source", "\"Source\" IN ('ofx', 'manual')");
                    table.CheckConstraint("ck_budget_operation_spread", "\"Spread\" >= 1 AND \"Spread\" <= 36");
                    table.ForeignKey(
                        name: "FK_operations_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_operations_lines_LineId_OwnerUserId",
                        columns: x => new { x.LineId, x.OwnerUserId },
                        principalSchema: "budget",
                        principalTable: "lines",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operations_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operations_rules_RuleId",
                        column: x => x.RuleId,
                        principalSchema: "budget",
                        principalTable: "rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_operations_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_HouseholdId_OwnerUserId",
                schema: "budget",
                table: "accounts",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_MemberProfileId_HouseholdId",
                schema: "budget",
                table: "accounts",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_OwnerUserId_Account",
                schema: "budget",
                table: "accounts",
                columns: new[] { "OwnerUserId", "Account" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lines_HouseholdId_OwnerUserId",
                schema: "budget",
                table: "lines",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_lines_MemberProfileId_HouseholdId",
                schema: "budget",
                table: "lines",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_lines_OwnerUserId",
                schema: "budget",
                table: "lines",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_operations_HouseholdId_OwnerUserId",
                schema: "budget",
                table: "operations",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_operations_LineId_OwnerUserId",
                schema: "budget",
                table: "operations",
                columns: new[] { "LineId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_operations_MemberProfileId_HouseholdId",
                schema: "budget",
                table: "operations",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_operations_OwnerUserId_Account_BankId",
                schema: "budget",
                table: "operations",
                columns: new[] { "OwnerUserId", "Account", "BankId" },
                unique: true,
                filter: "\"BankId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_operations_OwnerUserId_Date",
                schema: "budget",
                table: "operations",
                columns: new[] { "OwnerUserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_operations_RuleId",
                schema: "budget",
                table: "operations",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_rules_HouseholdId_OwnerUserId",
                schema: "budget",
                table: "rules",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_rules_LineId_OwnerUserId",
                schema: "budget",
                table: "rules",
                columns: new[] { "LineId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_rules_MemberProfileId_HouseholdId",
                schema: "budget",
                table: "rules",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_rules_OwnerUserId",
                schema: "budget",
                table: "rules",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts",
                schema: "budget");

            migrationBuilder.DropTable(
                name: "line_amounts",
                schema: "budget");

            migrationBuilder.DropTable(
                name: "operations",
                schema: "budget");

            migrationBuilder.DropTable(
                name: "rules",
                schema: "budget");

            migrationBuilder.DropTable(
                name: "lines",
                schema: "budget");
        }
    }
}
