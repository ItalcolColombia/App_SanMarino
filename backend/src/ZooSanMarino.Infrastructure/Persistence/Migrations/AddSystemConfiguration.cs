using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración para crear la tabla de configuraciones del sistema
    /// </summary>
    public partial class AddSystemConfiguration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemConfigurations",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigurations", x => x.Key);
                });

            // Insertar configuraciones por defecto
            migrationBuilder.InsertData(
                table: "SystemConfigurations",
                columns: new[] { "Key", "Value", "Description", "IsEncrypted", "CreatedAt" },
                values: new object[,]
                {
                    {
                        "SMTP_HOST",
                        "smtp.gmail.com",
                        "Host del servidor SMTP para envío de emails",
                        false,
                        DateTime.UtcNow
                    },
                    {
                        "SMTP_PORT",
                        "587",
                        "Puerto del servidor SMTP",
                        false,
                        DateTime.UtcNow
                    },
                    {
                        "SMTP_USERNAME",
                        "",
                        "Usuario SMTP (email)",
                        true,
                        DateTime.UtcNow
                    },
                    {
                        "SMTP_PASSWORD",
                        "",
                        "Contraseña SMTP",
                        true,
                        DateTime.UtcNow
                    },
                    {
                        "FROM_EMAIL",
                        "",
                        "Email de envío",
                        true,
                        DateTime.UtcNow
                    },
                    {
                        "FROM_NAME",
                        "Zoo San Marino",
                        "Nombre del remitente",
                        false,
                        DateTime.UtcNow
                    },
                    {
                        "JWT_SECRET_KEY",
                        "",
                        "Clave secreta para JWT",
                        true,
                        DateTime.UtcNow
                    }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemConfigurations");
        }
    }
}
