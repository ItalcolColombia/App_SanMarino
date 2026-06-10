using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Iteración 3 del módulo de tickets:
    ///  - Columnas Guid canónicas en <c>tickets</c> (en paralelo a los int, idempotente).
    ///  - Tabla <c>ticket_resolutores</c>: quién atiende qué tipo en qué país/empresa.
    ///  - Tabla <c>ticket_perfil_usuario</c>: nivel (Normal/Implementador) del solicitante.
    ///  - Tabla <c>ticket_resolutor_rol</c>: defaults de perfil de atención por rol.
    /// Todo idempotente (CLAUDE.md).
    /// </summary>
    public partial class AddTicketProfilesAndGuidIdentity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UP_SQL, suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down deja las tablas (soft-down); un rollback real las dropea.
            migrationBuilder.DropTable(name: "ticket_perfil_usuario",  schema: "public");
            migrationBuilder.DropTable(name: "ticket_resolutor_rol",    schema: "public");
            migrationBuilder.DropTable(name: "ticket_resolutores",      schema: "public");
            migrationBuilder.DropColumn(name: "assigned_to_user_guid",  schema: "public", table: "tickets");
            migrationBuilder.DropColumn(name: "created_by_user_guid",   schema: "public", table: "tickets");
        }

        private const string UP_SQL = @"
-- 1) Columnas Guid en tickets (ADD COLUMN IF NOT EXISTS — idempotente)
ALTER TABLE public.tickets
    ADD COLUMN IF NOT EXISTS assigned_to_user_guid uuid NULL,
    ADD COLUMN IF NOT EXISTS created_by_user_guid  uuid NULL;

-- 2) Tabla ticket_resolutores (quién atiende qué tipo y dónde)
CREATE TABLE IF NOT EXISTS public.ticket_resolutores (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id     uuid    NOT NULL,
    tipo        varchar(20) NOT NULL,
    pais_id     integer NULL,          -- NULL = global
    company_id  integer NOT NULL,
    activo      boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT timezone('utc', now()),
    updated_at  timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ticket_resolutores_user_tipo_pais_company
    ON public.ticket_resolutores (user_id, tipo, pais_id, company_id);
CREATE INDEX IF NOT EXISTS ix_ticket_resolutores_tipo        ON public.ticket_resolutores (tipo);
CREATE INDEX IF NOT EXISTS ix_ticket_resolutores_pais_id     ON public.ticket_resolutores (pais_id);
CREATE INDEX IF NOT EXISTS ix_ticket_resolutores_company_id  ON public.ticket_resolutores (company_id);
CREATE INDEX IF NOT EXISTS ix_ticket_resolutores_user_id     ON public.ticket_resolutores (user_id);

-- 3) Tabla ticket_perfil_usuario (nivel de solicitante: NORMAL | IMPLEMENTADOR)
CREATE TABLE IF NOT EXISTS public.ticket_perfil_usuario (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id     uuid    NOT NULL,
    company_id  integer NOT NULL,
    nivel       varchar(20) NOT NULL DEFAULT 'NORMAL',
    activo      boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT timezone('utc', now()),
    updated_at  timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ticket_perfil_usuario_user_company
    ON public.ticket_perfil_usuario (user_id, company_id);
CREATE INDEX IF NOT EXISTS ix_ticket_perfil_usuario_company_id ON public.ticket_perfil_usuario (company_id);

-- 4) Tabla ticket_resolutor_rol (defaults de perfil de atención por rol)
CREATE TABLE IF NOT EXISTS public.ticket_resolutor_rol (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    role_id     integer NOT NULL,
    tipo        varchar(20) NOT NULL,
    pais_id     integer NULL,
    company_id  integer NOT NULL,
    activo      boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT timezone('utc', now()),
    updated_at  timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ticket_resolutor_rol_role_tipo_pais_company
    ON public.ticket_resolutor_rol (role_id, tipo, pais_id, company_id);
CREATE INDEX IF NOT EXISTS ix_ticket_resolutor_rol_role_id    ON public.ticket_resolutor_rol (role_id);
CREATE INDEX IF NOT EXISTS ix_ticket_resolutor_rol_tipo       ON public.ticket_resolutor_rol (tipo);
CREATE INDEX IF NOT EXISTS ix_ticket_resolutor_rol_company_id ON public.ticket_resolutor_rol (company_id);
";
    }
}
