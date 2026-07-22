using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Backfill de solo-datos (sin cambios de schema): alinea el <c>lote_nombre</c> de los lotes de
    /// pollo engorde de <b>Panamá</b> que ya tienen un <c>lote_base_engorde_id</c> asignado pero que
    /// fueron creados ANTES de la feature de corrida (por eso su nombre es libre y <c>numero_corrida</c>
    /// está en NULL).
    ///
    /// Reusa la MISMA regla del runtime (<see cref="ZooSanMarino.Application.Calculos.GestionLotesEngordeCalculos"/>
    /// y <c>LoteAveEngordeService.CreateAsync</c>):
    ///   • numero_corrida = MAX por (company, lote_base, galpón) + posición (ROW_NUMBER por id) →
    ///     continúa DESPUÉS del máximo ya existente para no reusar números que la feature ya asignó.
    ///   • lote_nombre = trim(base) + " - " + numero_corrida   (ej. "94 - 1", "94 - 2").
    ///
    /// Idempotente: solo toca filas con <c>numero_corrida IS NULL</c> (las ya numeradas quedan intactas),
    /// así re-ejecutarla es no-op. Panamá se resuelve por NOMBRE del país (robusto a tilde/ID). No toca
    /// Ecuador/Colombia (nombre libre) ni lotes sin lote base (no hay de qué derivar el nombre).
    /// </summary>
    public partial class FixNombresLoteEngordePanamaPorLoteBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
WITH pan AS (
    -- País Panamá por nombre (robusto a tilde/ID; en la BD figura como 'Panama').
    SELECT pais_id FROM public.paises WHERE lower(pais_nombre) LIKE 'panam%'
),
grp_max AS (
    -- Máximo de corrida YA existente por grupo (incluye ya-numeradas y soft-deleted),
    -- igual que el MaxAsync del runtime (que no filtra deleted_at).
    SELECT company_id, lote_base_engorde_id, galpon_id,
           COALESCE(MAX(numero_corrida), 0) AS max_corr
    FROM public.lote_ave_engorde
    WHERE pais_id IN (SELECT pais_id FROM pan)
      AND lote_base_engorde_id IS NOT NULL
      AND galpon_id IS NOT NULL
    GROUP BY company_id, lote_base_engorde_id, galpon_id
),
target AS (
    -- Lotes de Panamá con base + galpón pero SIN numerar (creados antes de la feature).
    SELECT l.lote_ave_engorde_id AS id,
           l.company_id, l.lote_base_engorde_id AS base_id, l.galpon_id,
           ROW_NUMBER() OVER (
               PARTITION BY l.company_id, l.lote_base_engorde_id, l.galpon_id
               ORDER BY l.lote_ave_engorde_id
           ) AS rn
    FROM public.lote_ave_engorde l
    WHERE l.pais_id IN (SELECT pais_id FROM pan)
      AND l.lote_base_engorde_id IS NOT NULL
      AND l.galpon_id IS NOT NULL
      AND l.numero_corrida IS NULL
),
asign AS (
    SELECT t.id,
           (m.max_corr + t.rn) AS new_corr,
           btrim(COALESCE(b.nombre, '')) || ' - ' || (m.max_corr + t.rn)::text AS new_nombre
    FROM target t
    JOIN grp_max m
      ON m.company_id = t.company_id
     AND m.lote_base_engorde_id = t.base_id
     AND m.galpon_id = t.galpon_id
    JOIN public.lote_base_engorde b ON b.id = t.base_id
)
UPDATE public.lote_ave_engorde l
SET numero_corrida = a.new_corr,
    lote_nombre    = a.new_nombre
FROM asign a
WHERE l.lote_ave_engorde_id = a.id;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Backfill de una sola vía: no se guardaron los nombres libres previos, por lo que no hay
            // reverso exacto. No-op intencional (revertir renombraría sin poder restaurar el original).
        }
    }
}
