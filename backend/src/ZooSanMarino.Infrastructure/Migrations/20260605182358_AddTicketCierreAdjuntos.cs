using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCierreAdjuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente (CLAUDE.md): seguro ante re-runs / columnas creadas manualmente.
            migrationBuilder.Sql(@"
                ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS cerrado_por_user_id integer NULL;
                ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS correo_notificado_a character varying(255) NULL;
                ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS fecha_cierre_solicitante timestamp with time zone NULL;
                ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS fecha_notificacion_correo timestamp with time zone NULL;
                ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS notificado_correo boolean NOT NULL DEFAULT false;
                ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS solucion_descripcion text NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS public.ticket_adjuntos (
                    id                  bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
                    ticket_id           bigint NOT NULL,
                    tipo                character varying(20) NOT NULL,
                    contenido_base64    text NULL,
                    file_name           character varying(255) NULL,
                    content_type        character varying(120) NULL,
                    size_bytes          integer NULL,
                    url                 character varying(1000) NULL,
                    titulo              character varying(255) NULL,
                    created_by_user_id  integer NOT NULL,
                    created_at          timestamp with time zone NOT NULL DEFAULT timezone('utc', now()),
                    CONSTRAINT pk_ticket_adjuntos PRIMARY KEY (id),
                    CONSTRAINT fk_ticket_adjuntos_tickets_ticket_id FOREIGN KEY (ticket_id)
                        REFERENCES public.tickets (id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ix_ticket_adjuntos_ticket_id ON public.ticket_adjuntos (ticket_id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_adjuntos",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "cerrado_por_user_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "correo_notificado_a",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "fecha_cierre_solicitante",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "fecha_notificacion_correo",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "notificado_correo",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "solucion_descripcion",
                schema: "public",
                table: "tickets");
        }
    }
}
