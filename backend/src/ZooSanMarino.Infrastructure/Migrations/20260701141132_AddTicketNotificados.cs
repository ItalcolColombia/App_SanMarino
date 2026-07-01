using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketNotificados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente (CLAUDE.md): seguro ante re-runs / tablas creadas manualmente.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS public.ticket_notificados (
                    id                  bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
                    ticket_id           bigint NOT NULL,
                    user_guid           uuid NULL,
                    cedula              character varying(20) NULL,
                    email               character varying(255) NOT NULL,
                    nombre              character varying(255) NULL,
                    created_at          timestamp with time zone NOT NULL DEFAULT timezone('utc', now()),
                    created_by_user_id  integer NOT NULL,
                    CONSTRAINT pk_ticket_notificados PRIMARY KEY (id),
                    CONSTRAINT fk_ticket_notificados_tickets_ticket_id FOREIGN KEY (ticket_id)
                        REFERENCES public.tickets (id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ix_ticket_notificados_ticket_id ON public.ticket_notificados (ticket_id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_notificados",
                schema: "public");
        }
    }
}
