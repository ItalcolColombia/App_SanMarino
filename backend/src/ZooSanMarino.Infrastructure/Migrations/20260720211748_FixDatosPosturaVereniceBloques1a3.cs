using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Data-fix (DML, sin cambios de schema) de la Matriz Verenice rev 6-jul-26, Fase 0 — BLOQUES 1-3.
    /// Espejo EXACTO de backend/sql/fix_datos_postura_verenice_jul26.sql (bloques 1-3), convertido a
    /// migración para que se aplique solo en el deploy (Database__RunMigrations=true) sobre la RDS prod,
    /// versionado y auditable. Idempotente: cada bloque solo toca filas que siguen en el patrón corrupto
    /// (guardas en el WHERE) → re-aplicar es no-op. Alcance: Postura Colombia, company_id = 1.
    ///
    /// NO incluye el BLOQUE 4 (re-fechado de traslados 114→116): requiere la fecha real del movimiento
    /// confirmada por Verenice y se mantiene manual en el .sql.
    /// El BLOQUE 2.d (SELECT de auditoría) tampoco va: es una inspección de solo lectura sin sentido en
    /// una migración; correrlo a mano desde el .sql si se quiere el chequeo de regresión.
    ///
    /// GOTCHA (Bloque 1): el trigger trg_lotes_sync_lote_postura_levante reescribe aves_h/m_actual en
    /// CUALQUIER UPDATE de `lotes` con lotes.hembras_l/machos_l (NULL en 116/117) → se respaldan y
    /// restauran los saldos reales alrededor del UPDATE (1.a backup / 1.d restore).
    /// </summary>
    public partial class FixDatosPosturaVereniceBloques1a3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- ===== BLOQUE 1 — lotes 116/117 con fecha_encaset un año en el futuro =====
                -- 1.a) Backup defensivo de los saldos reales (el trigger los pisa en el UPDATE de `lotes`).
                DROP TABLE IF EXISTS _fix_lpl_aves_backup;
                CREATE TEMP TABLE _fix_lpl_aves_backup AS
                SELECT lote_postura_levante_id, aves_h_actual, aves_m_actual, aves_h_inicial, aves_m_inicial
                FROM lote_postura_levante
                WHERE lote_id IN (116, 117) AND company_id = 1;

                -- 1.b) Fix de fecha_encaset (idempotente: solo si sigue en el futuro).
                UPDATE lotes
                SET fecha_encaset = (SELECT fecha_encaset FROM lotes o WHERE o.lote_id = 114),
                    updated_at    = NOW() AT TIME ZONE 'utc'
                WHERE lote_id = 116 AND company_id = 1 AND fecha_encaset > now();

                UPDATE lotes
                SET fecha_encaset = (SELECT fecha_encaset FROM lotes o WHERE o.lote_id = 115),
                    updated_at    = NOW() AT TIME ZONE 'utc'
                WHERE lote_id = 117 AND company_id = 1 AND fecha_encaset > now();

                -- 1.c) Sincronizar la copia en lote_postura_levante (fallback idempotente si no hay trigger).
                UPDATE lote_postura_levante lpl
                SET fecha_encaset = l.fecha_encaset,
                    updated_at    = NOW() AT TIME ZONE 'utc'
                FROM lotes l
                WHERE lpl.lote_id = l.lote_id
                  AND lpl.lote_id IN (116, 117)
                  AND lpl.company_id = 1
                  AND lpl.fecha_encaset IS DISTINCT FROM l.fecha_encaset;

                -- 1.d) Restaurar los saldos reales que el trigger haya podido pisar en 1.b/1.c.
                UPDATE lote_postura_levante lpl
                SET aves_h_actual  = bak.aves_h_actual,
                    aves_m_actual  = bak.aves_m_actual,
                    aves_h_inicial = bak.aves_h_inicial,
                    aves_m_inicial = bak.aves_m_inicial
                FROM _fix_lpl_aves_backup bak
                WHERE lpl.lote_postura_levante_id = bak.lote_postura_levante_id
                  AND lpl.aves_h_actual IS DISTINCT FROM bak.aves_h_actual;

                DROP TABLE IF EXISTS _fix_lpl_aves_backup;

                -- ===== BLOQUE 2 — K345A/B (lotes 13/14) con ano_tabla_genetica=2023 → 2026 =====
                UPDATE lotes
                SET ano_tabla_genetica = 2026, updated_at = NOW() AT TIME ZONE 'utc'
                WHERE lote_id IN (13, 14) AND company_id = 1 AND ano_tabla_genetica = 2023;

                UPDATE lote_postura_levante lpl
                SET ano_tabla_genetica = l.ano_tabla_genetica, updated_at = NOW() AT TIME ZONE 'utc'
                FROM lotes l
                WHERE lpl.lote_id = l.lote_id AND lpl.lote_id IN (13, 14) AND lpl.company_id = 1
                  AND lpl.ano_tabla_genetica IS DISTINCT FROM l.ano_tabla_genetica;

                UPDATE lote_postura_produccion lpp
                SET ano_tabla_genetica = l.ano_tabla_genetica, updated_at = NOW() AT TIME ZONE 'utc'
                FROM lotes l
                WHERE lpp.lote_id = l.lote_id AND lpp.lote_id IN (13, 14) AND lpp.company_id = 1
                  AND lpp.ano_tabla_genetica IS DISTINCT FROM l.ano_tabla_genetica;

                -- ===== BLOQUE 3 — fecha_inicio_produccion desalineada del primer dato real =====
                WITH primeros AS (
                    SELECT lpp.lote_postura_produccion_id,
                        LEAST(lpp.fecha_inicio_produccion,
                            (SELECT MIN(sdp.fecha_registro) FROM seguimiento_diario_produccion sdp
                              WHERE sdp.lote_postura_produccion_id = lpp.lote_postura_produccion_id AND sdp.company_id = lpp.company_id),
                            (SELECT MIN(sdl.fecha) FROM seguimiento_diario_levante sdl
                              WHERE sdl.lote_postura_produccion_id = lpp.lote_postura_produccion_id AND sdl.tipo_seguimiento = 'produccion')
                        ) AS fecha_minima_real
                    FROM lote_postura_produccion lpp
                    WHERE lpp.deleted_at IS NULL AND lpp.company_id = 1 AND lpp.fecha_inicio_produccion IS NOT NULL
                )
                UPDATE lote_postura_produccion lpp
                SET fecha_inicio_produccion = pr.fecha_minima_real, updated_at = NOW() AT TIME ZONE 'utc'
                FROM primeros pr
                WHERE lpp.lote_postura_produccion_id = pr.lote_postura_produccion_id
                  AND pr.fecha_minima_real IS NOT NULL
                  AND pr.fecha_minima_real < lpp.fecha_inicio_produccion;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Corrección de datos NO auto-reversible: no se guardan los valores corruptos previos
            // (se tomó backup completo de la BD antes de aplicar). Down() es no-op a propósito.
        }
    }
}
