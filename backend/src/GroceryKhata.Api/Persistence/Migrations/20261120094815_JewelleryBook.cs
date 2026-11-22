using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JewelleryBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approval_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    memo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    piece_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gross_out = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    gross_back = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sold_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_items", x => x.id);
                    table.UniqueConstraint("AK_approval_items_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "approval_memos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    party = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    due_back = table.Column<DateOnly>(type: "date", nullable: false),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_memos", x => x.id);
                    table.UniqueConstraint("AK_approval_memos_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "girvi_loans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    items = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    metal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    gross = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    reading = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    fine = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    pledge_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    principal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    rate_per_month = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    day_wise = table.Column<bool>(type: "boolean", nullable: false),
                    pledged_on = table.Column<DateOnly>(type: "date", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    kyc = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_girvi_loans", x => x.id);
                    table.UniqueConstraint("AK_girvi_loans_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "girvi_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    interest_part = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    principal_part = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_girvi_payments", x => x.id);
                    table.UniqueConstraint("AK_girvi_payments_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "jewellery_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    metal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    purity = table.Column<int>(type: "integer", nullable: true),
                    weight = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    weight_out = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    fixed_rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    charge = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    advance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jewellery_orders", x => x.id);
                    table.UniqueConstraint("AK_jewellery_orders_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "scheme_instalments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheme_instalments", x => x.id);
                    table.UniqueConstraint("AK_scheme_instalments_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "scheme_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    redeemed_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheme_members", x => x.id);
                    table.UniqueConstraint("AK_scheme_members_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "scheme_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    months = table.Column<int>(type: "integer", nullable: false),
                    instalment = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    bonus = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheme_plans", x => x.id);
                    table.UniqueConstraint("AK_scheme_plans_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_items_shop_id_memo_id",
                table: "approval_items",
                columns: new[] { "shop_id", "memo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_items_shop_id_piece_id",
                table: "approval_items",
                columns: new[] { "shop_id", "piece_id" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_memos_shop_id_number",
                table: "approval_memos",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_girvi_loans_shop_id_created_at",
                table: "girvi_loans",
                columns: new[] { "shop_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_girvi_loans_shop_id_number",
                table: "girvi_loans",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_girvi_payments_shop_id_at",
                table: "girvi_payments",
                columns: new[] { "shop_id", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_girvi_payments_shop_id_loan_id",
                table: "girvi_payments",
                columns: new[] { "shop_id", "loan_id" });

            migrationBuilder.CreateIndex(
                name: "IX_jewellery_orders_shop_id_number",
                table: "jewellery_orders",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jewellery_orders_shop_id_status_due_on",
                table: "jewellery_orders",
                columns: new[] { "shop_id", "status", "due_on" });

            migrationBuilder.CreateIndex(
                name: "IX_scheme_instalments_shop_id_entry_id",
                table: "scheme_instalments",
                columns: new[] { "shop_id", "entry_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scheme_instalments_shop_id_member_id",
                table: "scheme_instalments",
                columns: new[] { "shop_id", "member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_scheme_members_shop_id_customer_id",
                table: "scheme_members",
                columns: new[] { "shop_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "IX_scheme_members_shop_id_number",
                table: "scheme_members",
                columns: new[] { "shop_id", "number" },
                unique: true);
        
            migrationBuilder.Sql("""
                ALTER TABLE girvi_loans ENABLE ROW LEVEL SECURITY;
                ALTER TABLE girvi_loans FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON girvi_loans
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE girvi_payments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE girvi_payments FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON girvi_payments
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE scheme_plans ENABLE ROW LEVEL SECURITY;
                ALTER TABLE scheme_plans FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON scheme_plans
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE scheme_members ENABLE ROW LEVEL SECURITY;
                ALTER TABLE scheme_members FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON scheme_members
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE scheme_instalments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE scheme_instalments FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON scheme_instalments
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE jewellery_orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE jewellery_orders FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON jewellery_orders
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE approval_memos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE approval_memos FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON approval_memos
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE approval_items ENABLE ROW LEVEL SECURITY;
                ALTER TABLE approval_items FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON approval_items
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_items");

            migrationBuilder.DropTable(
                name: "approval_memos");

            migrationBuilder.DropTable(
                name: "girvi_loans");

            migrationBuilder.DropTable(
                name: "girvi_payments");

            migrationBuilder.DropTable(
                name: "jewellery_orders");

            migrationBuilder.DropTable(
                name: "scheme_instalments");

            migrationBuilder.DropTable(
                name: "scheme_members");

            migrationBuilder.DropTable(
                name: "scheme_plans");
        }
    }
}
