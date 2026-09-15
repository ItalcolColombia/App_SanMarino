using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only, sin cambio de modelo): en las empresas SIN sufijo de corrida
    /// (<c>companies.nombre_lote_incluye_corrida = false</c>, Ecuador) el lote de engorde se llama como
    /// su lote base, siempre.
    ///
    /// <para>
    /// Hasta este commit <c>ConstruirNombreLote</c> ponía el sufijo desde la 2.ª corrida y la corrida
    /// contaba lotes borrados: un lote de prueba borrado en el galpón bastaba para que el real naciera
    /// <c>2604 - 2</c> (medido en la copia de prod del 14-sep-2026: 4 lotes vivos, Kilometro 22 y CAROLINA).
    /// El código ya nombra sin sufijo y numera solo lotes vivos; esto alinea lo que quedó escrito.
    /// </para>
    ///
    /// <para>
    /// <b>Alcance:</b> lotes vivos con base, galpón y corrida cuyo nombre es EXACTAMENTE
    /// <c>{base} - {numero_corrida}</c> (lo escribió el sufijo automático, no un técnico). Pasan a
    /// <c>{base}</c> y su corrida a la posición entre los vivos del mismo base+galpón. Se salta el lote si
    /// ya hay otro vivo con ese nombre en el galpón. Además se actualiza la etiqueta
    /// <c>· Lote {nombre}</c> de las referencias de sus gastos de inventario
    /// (<c>inventario_gestion_movimiento.reference</c> y <c>lote_registro_historico_unificado.referencia</c>):
    /// es texto de pantalla, la clave de lectura es el prefijo <c>Gasto inventario #N</c> y no cambia.
    /// Ningún trigger mira esas columnas.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente:</b> tras la 1.ª pasada ningún nombre cumple el patrón ⇒ no toca nada. Medición:
    /// <c>backend/sql/verificar_nombre_lote_engorde_sin_sufijo_corrida.sql</c>.
    /// </para>
    /// </summary>
    public partial class NombreLoteEngordeSinSufijoCorrida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                WITH vivos AS (
                    SELECT l.lote_ave_engorde_id,
                           l.company_id,
                           l.galpon_id,
                           l.lote_nombre,
                           l.numero_corrida,
                           btrim(b.nombre) AS base_nombre,
                           ROW_NUMBER() OVER (
                               PARTITION BY l.company_id, l.lote_base_engorde_id, l.galpon_id
                               ORDER BY l.fecha_encaset NULLS LAST, l.created_at, l.lote_ave_engorde_id
                           ) AS corrida_viva
                      FROM lote_ave_engorde l
                      JOIN companies c ON c.id = l.company_id AND c.nombre_lote_incluye_corrida = false
                      JOIN lote_base_engorde b ON b.id = l.lote_base_engorde_id
                     WHERE l.deleted_at IS NULL
                       AND l.galpon_id IS NOT NULL
                       AND l.numero_corrida IS NOT NULL
                ),
                objetivo AS (
                    SELECT v.*
                      FROM vivos v
                     WHERE v.base_nombre <> ''
                       AND v.lote_nombre = v.base_nombre || ' - ' || v.numero_corrida
                       AND NOT EXISTS (
                           SELECT 1
                             FROM lote_ave_engorde o
                            WHERE o.company_id = v.company_id
                              AND o.galpon_id = v.galpon_id
                              AND o.deleted_at IS NULL
                              AND o.lote_ave_engorde_id <> v.lote_ave_engorde_id
                              AND btrim(o.lote_nombre) = v.base_nombre)
                ),
                lotes AS (
                    UPDATE lote_ave_engorde l
                       SET lote_nombre = o.base_nombre,
                           numero_corrida = o.corrida_viva
                      FROM objetivo o
                     WHERE l.lote_ave_engorde_id = o.lote_ave_engorde_id
                    RETURNING l.lote_ave_engorde_id,
                              ' · Lote ' || o.lote_nombre AS sufijo_viejo,
                              ' · Lote ' || o.base_nombre AS sufijo_nuevo
                ),
                gastos AS (
                    SELECT 'Gasto inventario #' || g.id || ' ' AS prefijo,
                           x.sufijo_viejo,
                           x.sufijo_nuevo
                      FROM inventario_gasto g
                      JOIN lotes x ON x.lote_ave_engorde_id = g.lote_ave_engorde_id
                ),
                movimientos AS (
                    UPDATE inventario_gestion_movimiento m
                       SET reference = left(m.reference, length(m.reference) - length(g.sufijo_viejo)) || g.sufijo_nuevo
                      FROM gastos g
                     WHERE left(m.reference, length(g.prefijo)) = g.prefijo
                       AND right(m.reference, length(g.sufijo_viejo)) = g.sufijo_viejo
                    RETURNING m.id
                )
                UPDATE lote_registro_historico_unificado h
                   SET referencia = left(h.referencia, length(h.referencia) - length(g.sufijo_viejo)) || g.sufijo_nuevo
                  FROM gastos g
                 WHERE left(h.referencia, length(g.prefijo)) = g.prefijo
                   AND right(h.referencia, length(g.sufijo_viejo)) = g.sufijo_viejo;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversa a propósito: el sufijo era el defecto y el código ya no lo escribe; volver a
            // "2604 - 2" dejaría el nombre desalineado con lo que el backend nombra hoy.
        }
    }
}
