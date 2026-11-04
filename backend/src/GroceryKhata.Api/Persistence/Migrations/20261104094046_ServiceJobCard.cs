using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceJobCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "complaint",
                table: "jobs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "in_warranty",
                table: "jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "product_serial_id",
                table: "jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serial_no",
                table: "jobs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_jobs_shop_id_serial_no",
                table: "jobs",
                columns: new[] { "shop_id", "serial_no" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_jobs_shop_id_serial_no",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "complaint",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "in_warranty",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "product_serial_id",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "serial_no",
                table: "jobs");
        }
    }
}
