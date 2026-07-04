using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTokens : Migration
    {
        // IDEMPOTENTE (regla dura del repo): CREATE TABLE/INDEX IF NOT EXISTS.
        // Se aplica sola al arrancar la app (Database:RunMigrations). No se ejecuta contra ninguna BD a mano.
        // snake_case y tipos calcados del builder EF (id bigint IdentityAlways; timestamptz para fechas).
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.service_tokens (
    id            bigint GENERATED ALWAYS AS IDENTITY,
    name          character varying(120) NOT NULL,
    token_hash    character varying(64)  NOT NULL,
    user_id       uuid                   NOT NULL,
    scopes        character varying(500) NOT NULL,
    expires_at    timestamp with time zone NULL,
    revoked_at    timestamp with time zone NULL,
    last_used_at  timestamp with time zone NULL,
    created_at    timestamp with time zone NOT NULL,
    CONSTRAINT pk_service_tokens PRIMARY KEY (id)
);");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_service_tokens_user_id
    ON public.service_tokens (user_id);");

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ux_service_tokens_token_hash
    ON public.service_tokens (token_hash);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS public.service_tokens;");
        }
    }
}
