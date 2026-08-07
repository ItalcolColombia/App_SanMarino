using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Siembra en ItalJira el histórico REAL del desarrollo de la aplicación: los trabajos que
    /// nunca pasaron por un ticket porque nacieron y se resolvieron en el área de desarrollo.
    /// Sin esto, producción estrena el módulo vacío y parece que no se hizo nada.
    /// </summary>
    /// <remarks>
    /// <b>Fuente (real, no inventada):</b> los 198 documentos de <c>fase_de_desarrollo/</c> y sus
    /// fechas de git — alta del archivo (<c>git log --diff-filter=A</c>) como inicio y último toque
    /// (<c>git log -1</c>) como fin. El título de cada tarea es el encabezado H1 de su plan.
    ///
    /// <b>Agrupación:</b> 20 historias por módulo funcional (Postura levante/producción, Engorde,
    /// Reproductoras, Liquidación, Inventario, Carga masiva, Movimientos de aves, Reportes, Guías
    /// genéticas, Tickets, ItalJira, Implementación, Vacunación, Seguridad, Usuarios, Multi-empresa,
    /// Diseño, Integraciones y Plataforma). El TIPO sale de la naturaleza del plan:
    /// <c>fix_*</c>/<c>bug</c>/<c>correccion</c> ⇒ BUG · <c>refactor</c>/<c>mejora</c>/<c>upgrade</c>
    /// ⇒ MEJORA · contextos, diccionarios y specs ⇒ DOCUMENTACION · el resto ⇒ TAREA.
    ///
    /// <b>Estado:</b> todo queda en LISTO con sus fechas reales — el flujo completo hasta cerrado.
    /// La única historia que queda EN_CURSO es «ItalJira», porque es esta misma entrega y sería
    /// mentira darla por terminada antes de desplegarla.
    ///
    /// <b>Identidad:</b> el autor y responsable se resuelven POR EMAIL
    /// (<c>logins.email = 'moiesbbuga@gmail.com'</c>), nunca por un guid fijo: los ids difieren
    /// entre local y producción. Si el usuario no existe en el entorno, la migración <b>no siembra
    /// nada y no falla</b> (RAISE NOTICE + RETURN) — un seed no puede tumbar el arranque de la app.
    ///
    /// <b>Idempotencia:</b> las historias por <c>codigo</c> (<c>HIS-2026-NNNN</c>) y las tareas por
    /// <c>(historia_id, titulo)</c>, ambas con <c>WHERE NOT EXISTS</c>. Correrla dos veces no
    /// duplica una sola fila.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto. El SQL vive en el partial
    /// <c>.Seed.cs</c> por tamaño.
    /// </remarks>
    public partial class SeedHistorialDesarrolloItalJira : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }

        /// <summary>
        /// Revierte SOLO lo sembrado por esta migración, identificado por el rango de códigos
        /// <c>HIS-2026-0001..0020</c>. Las tareas se borran primero por la FK, y jamás se toca
        /// trabajo que alguien haya movido a otra historia después: el filtro es por
        /// <c>historia_id</c> vigente.
        /// </summary>
        private const string DOWN_SQL = @"
DELETE FROM public.ticket_tareas
WHERE historia_id IN (
    SELECT id FROM public.historias
    WHERE codigo ~ '^HIS-2026-00(0[1-9]|1[0-9]|20)$'
);

UPDATE public.tickets SET historia_id = NULL
WHERE historia_id IN (
    SELECT id FROM public.historias
    WHERE codigo ~ '^HIS-2026-00(0[1-9]|1[0-9]|20)$'
);

DELETE FROM public.historias
WHERE codigo ~ '^HIS-2026-00(0[1-9]|1[0-9]|20)$';
";
    }
}
