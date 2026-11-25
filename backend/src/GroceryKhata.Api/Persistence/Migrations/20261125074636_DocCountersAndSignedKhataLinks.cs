using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocCountersAndSignedKhataLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One counter per shop and kind of numbered paper (estimates, jobs, girvi loans...): see DocumentNumbers.
            // Each counter starts from the highest number already saved the first time it is used.
            migrationBuilder.Sql("""
                CREATE TABLE doc_counters (
                    shop_id uuid NOT NULL REFERENCES shops (id) ON DELETE CASCADE,
                    kind varchar(80) NOT NULL,
                    last_number integer NOT NULL,
                    PRIMARY KEY (shop_id, kind));

                ALTER TABLE doc_counters ENABLE ROW LEVEL SECURITY;
                ALTER TABLE doc_counters FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON doc_counters
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            // Khata links are now signed with a server key, so the database no longer holds what opens them. A link
            // already sent keeps working until it expires: its token is kept only as a hash (KhataViewEndpoints.LegacyHash).
            // The table owner skips row-level security while this runs (NO FORCE), so every shop's customers are done.
            migrationBuilder.Sql("""
                ALTER TABLE customers NO FORCE ROW LEVEL SECURITY;
                UPDATE customers SET khata_token = 'h:' || encode(sha256(convert_to(khata_token, 'UTF8')), 'base64')
                WHERE khata_token IS NOT NULL AND khata_token NOT LIKE 'h:%';
                ALTER TABLE customers FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hashed and signed khata tokens can't be turned back into the old ones: those links stop.
            migrationBuilder.Sql("""
                ALTER TABLE customers NO FORCE ROW LEVEL SECURITY;
                UPDATE customers SET khata_token = NULL, khata_token_at = NULL WHERE khata_token IS NOT NULL;
                ALTER TABLE customers FORCE ROW LEVEL SECURITY;
                DROP TABLE doc_counters;
                """);
        }
    }
}
