using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSesionesActivas : Migration
    {
        // B1 — revocación de sesión. Tabla de sesiones vivas (lista BLANCA por jti).
        //
        // IDEMPOTENTE (regla dura del repo): CREATE TABLE/INDEX IF NOT EXISTS. Se aplica sola al
        // arrancar la app (Database:RunMigrations) y la BD local la comparten varias sesiones.
        // DDL puro: sin DML, sin FK y sin triggers → nada que pueda correr contra datos inexistentes
        // ni tumbar el arranque en ECS (una migración que falla ahí es SIGSEGV + rollback silencioso).
        //
        // SIN FK a users, igual que service_tokens: un ON DELETE mal elegido convertiría el borrado
        // de un usuario en un error de runtime. La integridad la garantiza el service (el user_id
        // sale del token ya validado).
        //
        // snake_case y tipos calcados del builder EF (id bigint IdentityAlways; timestamptz).
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.sesiones_activas (
    id                 bigint GENERATED ALWAYS AS IDENTITY,
    jti                uuid                     NOT NULL,
    user_id            uuid                     NOT NULL,
    device_id          character varying(100)   NULL,
    ip_address         character varying(64)    NULL,
    user_agent         character varying(300)   NULL,
    created_at         timestamp with time zone NOT NULL,
    expires_at         timestamp with time zone NOT NULL,
    last_seen_at       timestamp with time zone NULL,
    revoked_at         timestamp with time zone NULL,
    revoked_by_user_id uuid                     NULL,
    revoked_reason     character varying(200)   NULL,
    CONSTRAINT pk_sesiones_activas PRIMARY KEY (id)
);");

            // Se busca por jti en CADA request autenticado → único.
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ux_sesiones_activas_jti
    ON public.sesiones_activas (jti);");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_sesiones_activas_user_id
    ON public.sesiones_activas (user_id);");

            // El listado de la UI y la limpieza sólo miran sesiones vivas.
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_sesiones_activas_vivas
    ON public.sesiones_activas (user_id, expires_at)
    WHERE revoked_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS public.sesiones_activas;");
        }
    }
}
