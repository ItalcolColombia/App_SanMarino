using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// A5 (primera parte) de <c>fase_de_desarrollo/f0a_auditoria_estado_2026-08-09.md</c>:
    /// <b>lápidas de borrado</b> para las cuatro tablas operativas.
    ///
    /// <para>
    /// <b>El problema.</b> Los borrados de seguimiento e inventario son <b>físicos</b>: la fila
    /// desaparece y no queda ni un rastro. Para cualquier lectura por cursor —un dispositivo que
    /// pregunta "¿qué cambió desde la última vez?"— eso es invisible: un `updated_at` no puede
    /// transportar una fila que ya no existe. El resultado es que el dispositivo se queda con el dato
    /// fantasma <b>para siempre</b>, mostrando un seguimiento o un movimiento que en el servidor ya
    /// no está. Es el bloqueante duro de la sincronización, y no se puede arreglar después: lo que se
    /// borró sin dejar lápida ya no se puede reconstruir.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué esto NO cambia ningún comportamiento.</b> Es puramente aditivo: una tabla nueva y
    /// un trigger <c>AFTER DELETE</c> que solo inserta en ella. No hay soft delete, no hay filtro
    /// global de consulta, no hay una línea de C# que lea esto todavía. Los borrados siguen
    /// funcionando exactamente igual — ahora además dejan constancia. Se despliega hoy justamente
    /// para que, cuando la sincronización exista, ya haya historia de borrados en vez de arrancar de
    /// cero.
    /// </para>
    ///
    /// <para>
    /// <b>Qué se guarda y qué no.</b> Solo el id, la empresa/granja cuando la fila las tiene, y las
    /// <b>claves de negocio</b> (lote, fecha, ubicación, ítem). Deliberadamente <b>no</b> se guarda la
    /// fila entera: el objetivo es que un cliente sepa <i>qué</i> se borró y a qué lote y fecha
    /// correspondía, no conservar el dato borrado — eso sería una copia paralela de datos operativos
    /// que nadie audita y que crece sin control.
    /// </para>
    /// </summary>
    public partial class AddSyncTombstones : Migration
    {
        /// <summary>Las cuatro tablas operativas cuyos borrados hoy son invisibles.</summary>
        private static readonly string[] TablasOperativas =
        {
            "seguimiento_diario_levante",
            "seguimiento_diario_produccion",
            "seguimiento_diario_aves_engorde",
            "inventario_gestion_movimiento"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Tabla ───────────────────────────────────────────────────────────────────
            // No tiene entidad de EF a propósito: nada del dominio la lee, así que modelarla solo
            // agregaría ruido al ModelSnapshot. La consumirá el endpoint de sincronización.
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.sync_tombstones (
    id           bigserial   PRIMARY KEY,
    tabla        text        NOT NULL,
    registro_id  bigint      NOT NULL,
    company_id   integer     NULL,
    farm_id      integer     NULL,
    clave        jsonb       NOT NULL DEFAULT '{}'::jsonb,
    borrado_at   timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE public.sync_tombstones IS
    'Lapidas de borrado de las tablas operativas. Los borrados son fisicos: sin esto, un cliente que sincroniza por cursor nunca se entera y se queda con el dato fantasma para siempre.';

-- Por borrado_at: es el orden del cursor de sincronizacion (dame lo borrado desde X).
CREATE INDEX IF NOT EXISTS ix_sync_tombstones_borrado_at
    ON public.sync_tombstones (borrado_at);

-- Por (tabla, registro_id): para responder ""esta fila que tengo, sigue viva?"".
CREATE INDEX IF NOT EXISTS ix_sync_tombstones_tabla_registro
    ON public.sync_tombstones (tabla, registro_id);

-- Por empresa: el pull siempre esta particionado por empresa (nunca se devuelven lapidas de otra).
CREATE INDEX IF NOT EXISTS ix_sync_tombstones_company
    ON public.sync_tombstones (company_id);
");

            // ── Función del trigger ─────────────────────────────────────────────────────
            // Genérica: sirve para las cuatro tablas (y para las que se agreguen) porque saca las
            // claves de `to_jsonb(OLD)` en vez de nombrar columnas que no existen en todas.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.trg_sync_tombstone()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
DECLARE
    v      jsonb := to_jsonb(OLD);
    claves text[] := ARRAY[
        'lote_id', 'lote_postura_levante_id', 'lote_postura_produccion_id',
        'lote_ave_engorde_id', 'fecha_registro', 'fecha', 'fecha_operacion',
        'item_inventario_ecuador_id', 'nucleo_id', 'galpon_id',
        'numero_movimiento', 'movement_type'
    ];
    v_clave jsonb;
BEGIN
    -- Solo claves de NEGOCIO. Guardar la fila entera convertiria esto en una copia paralela de
    -- datos operativos que nadie audita y que crece sin control.
    SELECT COALESCE(jsonb_object_agg(k, v -> k), '{}'::jsonb)
      INTO v_clave
      FROM unnest(claves) AS k
     WHERE v ? k AND v -> k <> 'null'::jsonb;

    INSERT INTO public.sync_tombstones (tabla, registro_id, company_id, farm_id, clave)
    VALUES (
        TG_TABLE_NAME,
        (v ->> 'id')::bigint,
        NULLIF(v ->> 'company_id', '')::integer,
        NULLIF(v ->> 'farm_id', '')::integer,
        v_clave
    );

    RETURN OLD;
END;
$fn$;
");

            // ── Triggers ────────────────────────────────────────────────────────────────
            // `DROP ... IF EXISTS` + `CREATE` en vez de `CREATE OR REPLACE TRIGGER` para que la
            // migración sea idempotente también en versiones de Postgres que no soportan el REPLACE.
            foreach (var tabla in TablasOperativas)
            {
                migrationBuilder.Sql($@"
DROP TRIGGER IF EXISTS trg_tombstone_{tabla} ON public.{tabla};
CREATE TRIGGER trg_tombstone_{tabla}
    AFTER DELETE ON public.{tabla}
    FOR EACH ROW EXECUTE FUNCTION public.trg_sync_tombstone();
");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tabla in TablasOperativas)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_tombstone_{tabla} ON public.{tabla};");
            }

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.trg_sync_tombstone();");

            // La tabla NO se borra: contiene la historia de borrados, que es justamente lo
            // irrecuperable. Revertir el mecanismo no es razón para destruir lo ya registrado.
        }
    }
}
