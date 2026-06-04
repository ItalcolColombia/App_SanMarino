using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tickets",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pais_id = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    assigned_to_user_id = table.Column<int>(type: "integer", nullable: true),
                    fecha_primera_apertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_solucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false, defaultValue: "A"),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tickets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_imagenes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    imagen_base64 = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    size_bytes = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_imagenes", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_imagenes_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_notas",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    nota = table.Column<string>(type: "text", nullable: false),
                    estado_resultante = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    es_interna = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_notas", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_notas_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_imagenes_ticket_id",
                schema: "public",
                table: "ticket_imagenes",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_notas_created_at",
                schema: "public",
                table: "ticket_notas",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_notas_ticket_id",
                schema: "public",
                table: "ticket_notas",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_assigned_to_user_id",
                schema: "public",
                table: "tickets",
                column: "assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_codigo",
                schema: "public",
                table: "tickets",
                column: "codigo");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_company_id",
                schema: "public",
                table: "tickets",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_created_at",
                schema: "public",
                table: "tickets",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_created_by_user_id",
                schema: "public",
                table: "tickets",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_estado",
                schema: "public",
                table: "tickets",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_pais_id",
                schema: "public",
                table: "tickets",
                column: "pais_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tipo",
                schema: "public",
                table: "tickets",
                column: "tipo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_imagenes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_notas",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "public");
        }
    }
}
