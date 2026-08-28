using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Deja que la corrección del encasetamiento de un lote de engorde pueda GUARDARSE: amplía el
    /// catálogo de <c>historial_lote_pollo_engorde.tipo_registro</c> a los cuatro valores que el
    /// código escribe y relaja <c>ck_hlpe_aves_nonneg</c> para el único tipo que guarda un delta.
    ///
    /// <para>
    /// <b>El bug.</b> Desde <c>a9fd721</c> (21-ago-2026) editar las aves de un lote de engorde audita
    /// el ajuste con <c>tipo_registro = 'AjusteEncaset'</c>
    /// (<c>LoteAveEngordeService.AplicarAjusteEncasetamientoAsync</c>). La tabla, en producción, tenía
    /// <c>CHECK (tipo_registro IN ('Inicio','Ajuste','AjusteResync'))</c> —lista que dejó
    /// <c>20260611172121_CorreccionSaldosAvesEngorde2601y2602</c>— así que ese INSERT moría con
    /// SQLSTATE <b>23514</b> y arrastraba la transacción entera: no se guardaba ni el <c>Inicio</c>
    /// corregido, ni <c>aves_encasetadas</c>, ni el saldo. El usuario sólo veía el toast genérico
    /// «Alguno de los valores no cumple una regla de validación de la base de datos»
    /// (<c>ErrorPersistenciaCalculos</c>). La funcionalidad se mergeó con el C# que escribe el valor y
    /// sin la migración que lo permite: el <c>.sql</c> es el espejo, la migración es el vehículo.
    /// </para>
    ///
    /// <para>
    /// <b>Y el caso simétrico.</b> La fila de auditoría guarda el <b>delta con signo</b>, negativo
    /// cuando el ajuste QUITA aves, contra un <c>ck_hlpe_aves_nonneg CHECK (aves_hembras >= 0 AND
    /// aves_machos >= 0 AND aves_mixtas >= 0)</c>. Arreglar sólo el primer CHECK dejaba «restar aves»
    /// fallando con el mismo 23514 inútil. Es un solo defecto con dos caras, y van juntos.
    /// La constraint se <b>relaja</b>, no se borra: <c>Inicio</c>, <c>Ajuste</c> y <c>AjusteResync</c>
    /// son CANTIDADES y siguen sin poder ser negativas.
    /// </para>
    ///
    /// <para>
    /// <b>Inerte para los lectores.</b> Los seis consumidores de la tabla filtran <c>tipo_registro</c>
    /// explícitamente (<c>= 'Inicio'</c> o <c>= 'Ajuste'</c>): <c>ProjectToDetail</c>,
    /// <c>ResumenDisponibilidad</c> (×2), <c>LiquidacionCongeladaAplicador</c>,
    /// <c>CorreccionAvesDisponiblesEngordeService</c> y <c>fn_cuadre_aves_engorde</c>. Ninguno suma la
    /// tabla entera ⇒ una fila <c>AjusteEncaset</c>, positiva o negativa, no mueve un solo número.
    /// Mismo criterio con que se incorporó <c>AjusteResync</c> en junio.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente y fail-soft.</b> <c>tipo_registro</c> se recrea sólo si ninguna fila viola el
    /// catálogo nuevo (imposible si la constraint ya existe: el catálogo nuevo es un superconjunto del
    /// viejo); si hubiera basura no se toca nada y queda un <c>RAISE WARNING</c> en el log del deploy.
    /// <c>aves_nonneg</c> se reemplaza sólo donde YA existe — relajar un predicado nunca puede fallar
    /// sobre datos existentes, y donde no exista esta migración no inventa un invariante nuevo.
    /// Jamás se tira el arranque de producción por esto (lección del incidente SIGSEGV).
    /// </para>
    ///
    /// <para>
    /// Catálogo espejo en C#: <c>TipoRegistroHistorialEngordeCalculos</c> (con tests que congelan esta
    /// misma lista). DDL espejo: <c>backend/sql/create_historial_lote_pollo_engorde.sql</c>.
    /// Plan: <c>fase_de_desarrollo/ajuste_encasetamiento_engorde_check_tipo_registro_plan.md</c>.
    /// </para>
    /// </summary>
    public partial class AmpliaCheckHistorialEngordeAjusteEncaset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UP_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }

        private const string UP_SQL = @"
DO $$
DECLARE
    v_fuera_de_catalogo bigint;
BEGIN
    IF to_regclass('public.historial_lote_pollo_engorde') IS NULL THEN
        RAISE WARNING 'historial_lote_pollo_engorde no existe: nada que ampliar.';
        RETURN;
    END IF;

    -- ── 1) tipo_registro: 'Inicio' | 'Ajuste' | 'AjusteResync' | 'AjusteEncaset' ──────────
    -- 'AjusteEncaset' = auditoria de la correccion del encasetamiento (delta con signo).
    -- No participa en la conservacion: ya esta dentro del registro 'Inicio' corregido.
    SELECT count(*) INTO v_fuera_de_catalogo
    FROM public.historial_lote_pollo_engorde
    WHERE tipo_registro IS NULL
       OR tipo_registro NOT IN ('Inicio', 'Ajuste', 'AjusteResync', 'AjusteEncaset');

    IF v_fuera_de_catalogo = 0 THEN
        ALTER TABLE public.historial_lote_pollo_engorde
            DROP CONSTRAINT IF EXISTS ck_hlpe_tipo_registro;
        ALTER TABLE public.historial_lote_pollo_engorde
            ADD CONSTRAINT ck_hlpe_tipo_registro
            CHECK (tipo_registro IN ('Inicio', 'Ajuste', 'AjusteResync', 'AjusteEncaset'));
    ELSE
        -- Fail-soft: se deja la constraint como este y se avisa. Nunca se tira el arranque.
        RAISE WARNING 'ck_hlpe_tipo_registro NO se recreo: % fila(s) con tipo_registro fuera del catalogo.',
            v_fuera_de_catalogo;
    END IF;

    -- ── 2) aves_nonneg: el delta de 'AjusteEncaset' puede ser negativo ────────────────────
    -- Se REEMPLAZA solo donde ya existe (relajar un predicado no puede fallar sobre datos
    -- existentes). Donde no exista, no se crea: esta migracion no inventa invariantes nuevos.
    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_hlpe_aves_nonneg'
          AND conrelid = 'public.historial_lote_pollo_engorde'::regclass
    ) THEN
        ALTER TABLE public.historial_lote_pollo_engorde
            DROP CONSTRAINT ck_hlpe_aves_nonneg;
        ALTER TABLE public.historial_lote_pollo_engorde
            ADD CONSTRAINT ck_hlpe_aves_nonneg
            CHECK (
                tipo_registro = 'AjusteEncaset'
                OR (aves_hembras >= 0 AND aves_machos >= 0 AND aves_mixtas >= 0)
            );
    END IF;
END $$;

COMMENT ON COLUMN public.historial_lote_pollo_engorde.tipo_registro IS
    'Inicio = aves con que arranco el lote; Ajuste = descuento por aves fantasma (participa en la conservacion); AjusteResync = sustituye el descuento de ventas que no descontaron; AjusteEncaset = correccion del encasetamiento, guarda el DELTA con signo y no participa en la conservacion.';
";

        private const string DOWN_SQL = @"
DO $$
DECLARE
    v_bloqueantes bigint;
BEGIN
    IF to_regclass('public.historial_lote_pollo_engorde') IS NULL THEN
        RETURN;
    END IF;

    -- Volver al catalogo de 3 valores solo es posible si no quedo ninguna fila 'AjusteEncaset'
    -- ni ningun delta negativo: revertir borrando auditoria seria falsificar el historico.
    SELECT count(*) INTO v_bloqueantes
    FROM public.historial_lote_pollo_engorde
    WHERE tipo_registro = 'AjusteEncaset'
       OR aves_hembras < 0 OR aves_machos < 0 OR aves_mixtas < 0;

    IF v_bloqueantes > 0 THEN
        RAISE WARNING 'Down() no revierte los CHECK: % fila(s) de ajuste de encasetamiento en la tabla.',
            v_bloqueantes;
        RETURN;
    END IF;

    ALTER TABLE public.historial_lote_pollo_engorde
        DROP CONSTRAINT IF EXISTS ck_hlpe_tipo_registro;
    ALTER TABLE public.historial_lote_pollo_engorde
        ADD CONSTRAINT ck_hlpe_tipo_registro
        CHECK (tipo_registro IN ('Inicio', 'Ajuste', 'AjusteResync'));

    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_hlpe_aves_nonneg'
          AND conrelid = 'public.historial_lote_pollo_engorde'::regclass
    ) THEN
        ALTER TABLE public.historial_lote_pollo_engorde
            DROP CONSTRAINT ck_hlpe_aves_nonneg;
        ALTER TABLE public.historial_lote_pollo_engorde
            ADD CONSTRAINT ck_hlpe_aves_nonneg
            CHECK (aves_hembras >= 0 AND aves_machos >= 0 AND aves_mixtas >= 0);
    END IF;
END $$;
";
    }
}
