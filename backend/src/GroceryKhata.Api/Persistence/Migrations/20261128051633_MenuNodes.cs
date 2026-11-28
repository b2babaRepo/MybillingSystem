using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MenuNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_nodes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    parent_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    name_hi = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    name_en = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    name_gu = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    words = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    icon = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    picture_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    picture = table.Column<byte[]>(type: "bytea", nullable: true),
                    picture_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_nodes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_menu_nodes_trade_key",
                table: "menu_nodes",
                columns: new[] { "trade", "key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menu_nodes");
        }
    }
}
