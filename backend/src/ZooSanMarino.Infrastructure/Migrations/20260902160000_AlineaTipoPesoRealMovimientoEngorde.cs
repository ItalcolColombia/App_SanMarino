using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Alinea el tipo de <c>movimiento_pollo_engorde.peso_bruto_real</c> y <c>peso_tara_real</c>:
    /// <c>numeric(12,3)</c> → <c>double precision</c>, que es lo que dice el modelo.
    /// </summary>
    /// <remarks>
    /// <b>De dónde venía la divergencia.</b> La migración que las crea
    /// (<c>20260521110000_AddPesosRealesMovimientoEngorde</c>) nació sin su <c>.Designer.cs</c>, o sea
    /// invisible para EF, y las columnas se aplicaron a mano con
    /// <c>backend/sql/apply_pesos_reales_movimiento_engorde.sql</c>, que eligió <c>NUMERIC(12,3)</c>.
    /// El modelo las declara <c>double?</c> ⇒ <c>double precision</c>. Resultado: en las bases que
    /// pasaron por el script el peso se redondeaba a 3 decimales <b>en la columna</b>, y en una base
    /// creada desde migraciones no.
    ///
    /// <b>Por qué gana el modelo.</b> Las otras <b>6</b> columnas <c>peso_*</c> de la misma tabla
    /// —<c>peso_bruto</c>, <c>peso_tara</c>, <c>peso_neto</c> y sus <c>_global</c>— ya son
    /// <c>double precision</c>: estas dos eran las únicas distintas, y por accidente. Alinear la BD
    /// al modelo no toca ni una línea de C# (el tipo CLR sigue siendo <c>double?</c> en la entidad,
    /// los DTOs y los 6 services); alinear al revés obligaría a pasar todo a <c>decimal</c>.
    ///
    /// <b>El redondeo a 3 decimales NO se pierde: nunca vivió acá.</b> Lo hace
    /// <c>MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea</c>, con <c>Math.Round(…, 3)</c>
    /// sobre bruto, tara, neto y el residuo. Medido antes de tocar nada: <b>0 filas</b> de
    /// <c>peso_bruto</c> y <c>peso_tara</c> tienen más de 3 decimales, así que el otro camino que
    /// escribe estas columnas (<c>OrganizarPeso</c>, que copia <c>PesoBruto</c>/<c>PesoTara</c> tal
    /// cual) tampoco depende del recorte de la columna.
    ///
    /// <b>El trigger hay que sacarlo y volver a ponerlo.</b>
    /// <c>trg_movimiento_pollo_engorde_lote_hist</c> es <c>AFTER INSERT OR UPDATE OF … peso_tara_real
    /// …</c>: una lista de columnas explícita fija la columna y Postgres rechaza el <c>ALTER TYPE</c>
    /// con <i>«cannot alter type of a column used in a trigger definition»</i>. Se recrea desde
    /// <c>pg_get_triggerdef</c> —o sea desde la versión <b>realmente desplegada</b>, no desde un
    /// literal del repo— y todo va en la misma transacción, así que no hay ventana en la que el
    /// histórico unificado deje de llenarse.
    ///
    /// <b>Gate corrido antes de escribir esto</b> (todo en transacción con <c>ROLLBACK</c>):
    /// <c>fn_seguimiento_diario_engorde</c> para los <b>184</b> lotes del histórico, <b>6.789 filas,
    /// 0 diferencias</b>; los dos pesos comparados fila a fila, <b>0 diferencias exactas</b> (no
    /// redondeadas); trigger recreado <b>idéntico</b> y los 2 triggers de la tabla vivos. Que la fn
    /// no se mueva era esperable y quedó medido: lee <c>peso_tara_real</c> de
    /// <c>lote_registro_historico_unificado</c>, que sigue siendo <c>numeric(18,3)</c> — el recorte a
    /// 3 decimales del histórico no lo toca esta migración.
    ///
    /// Idempotente: el bloque entero solo corre si alguna de las dos sigue en <c>numeric</c>.
    /// Plan: <c>fase_de_desarrollo/alinear_modelsnapshot_ef_plan.md</c> (sección «Fase 3»).
    /// </remarks>
    public partial class AlineaTipoPesoRealMovimientoEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(CambiarTipo("double precision", "numeric"));

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(CambiarTipo("numeric(12,3)", "double precision"));

        /// <summary>
        /// Cambia las dos columnas a <paramref name="tipoDestino"/>, pero solo si alguna todavía
        /// está en <paramref name="tipoActual"/> (por eso es idempotente en los dos sentidos).
        /// Los triggers que nombran alguna de las dos columnas se guardan tal como están
        /// desplegados, se borran y se restauran literales en la misma transacción.
        /// </summary>
        private static string CambiarTipo(string tipoDestino, string tipoActual) => $@"
DO $peso$
DECLARE
    v_nombres text[];
    v_defs    text[];
    v_i       int;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name   = 'movimiento_pollo_engorde'
           AND column_name IN ('peso_bruto_real', 'peso_tara_real')
           AND data_type    = '{tipoActual}')
    THEN
        RAISE NOTICE 'peso_bruto_real/peso_tara_real ya no son {tipoActual}: no hay nada que hacer';
        RETURN;
    END IF;

    -- Los triggers con lista de columnas explicita FIJAN la columna: sin sacarlos, el ALTER TYPE
    -- falla. Se guardan como estan DESPLEGADOS, no como los imagina el repo.
    SELECT COALESCE(array_agg(t.tgname::text          ORDER BY t.tgname), ARRAY[]::text[]),
           COALESCE(array_agg(pg_get_triggerdef(t.oid) ORDER BY t.tgname), ARRAY[]::text[])
      INTO v_nombres, v_defs
      FROM pg_trigger t
      JOIN pg_class c     ON c.oid = t.tgrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE n.nspname = 'public'
       AND c.relname = 'movimiento_pollo_engorde'
       AND NOT t.tgisinternal
       AND (pg_get_triggerdef(t.oid) LIKE '%peso_bruto_real%'
         OR pg_get_triggerdef(t.oid) LIKE '%peso_tara_real%');

    FOR v_i IN 1 .. COALESCE(array_length(v_nombres, 1), 0) LOOP
        EXECUTE format('DROP TRIGGER %I ON public.movimiento_pollo_engorde', v_nombres[v_i]);
    END LOOP;

    ALTER TABLE public.movimiento_pollo_engorde
        ALTER COLUMN peso_bruto_real TYPE {tipoDestino} USING peso_bruto_real::{tipoDestino};
    ALTER TABLE public.movimiento_pollo_engorde
        ALTER COLUMN peso_tara_real  TYPE {tipoDestino} USING peso_tara_real::{tipoDestino};

    FOR v_i IN 1 .. COALESCE(array_length(v_defs, 1), 0) LOOP
        EXECUTE v_defs[v_i];
    END LOOP;
END
$peso$;
";
    }
}
