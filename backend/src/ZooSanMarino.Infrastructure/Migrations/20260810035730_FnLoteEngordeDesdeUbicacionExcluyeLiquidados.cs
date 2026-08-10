using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// A9 (paso 2): <c>fn_lote_ave_engorde_id_desde_ubicacion</c> deja de imputar movimientos a
    /// lotes <b>liquidados</b>.
    ///
    /// <para>
    /// <b>Regla de negocio (decisión del usuario, 09-ago-2026):</b> un lote liquidado está
    /// congelado. No recibe atribución nueva. La liquidación guarda una <i>copia congelada</i> de los
    /// números del lote; si después le siguen entrando movimientos, esa copia y el dato vivo dejan de
    /// coincidir y no hay forma de saber cuál de los dos es el bueno.
    /// </para>
    ///
    /// <para>
    /// <b>Qué hacía mal.</b> La función resolvía con <c>ORDER BY lote_ave_engorde_id DESC LIMIT 1</c>
    /// —el id más alto del galpón, sin mirar nada más— y el trigger del histórico la llama en cada
    /// INSERT. En un galpón que encadena ciclos (en Ecuador, <b>34 de 35</b>) eso significa que un
    /// movimiento puede caer sobre un lote ya liquidado.
    /// </para>
    ///
    /// <para>
    /// <b>Radio de impacto, medido antes de tocar</b> (BD local, refresh del dump de prod):
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>Panamá: 38 de 38 galpones sin ningún cambio.</b> El gate multipaís se
    ///   cumple por construcción — Panamá no tiene un solo lote cerrado.</description></item>
    ///   <item><description>Ecuador: 25 de 35 galpones sin cambio; <b>10 pasan a NULL</b> (son los que
    ///   hoy no tienen ningún lote abierto).</description></item>
    ///   <item><description><b>0 galpones pasan a imputar a un lote distinto.</b> La regla nunca
    ///   redirige alimento de un lote a otro: solo deja de cargárselo a uno liquidado.</description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Por qué NULL es seguro y no "el movimiento se vuelve invisible".</b> Es la pregunta que
    /// importa, porque dejar movimientos invisibles es como se rompieron intentos anteriores en esta
    /// misma zona. Verificado: <c>fn_seguimiento_diario_engorde</c> trata ese caso explícitamente
    /// —"los movimientos sin lote (<c>lote_ave_engorde_id IS NULL</c>) se conservan: no se pierde
    /// alimento"— y hoy ya hay <b>1.453 filas</b> de Ecuador en ese estado. No es un estado nuevo ni
    /// excepcional: es el mismo por el que pasa el alimento que llega antes del encaset, y que la
    /// apertura del ciclo siguiente recoge.
    /// </para>
    ///
    /// <para>
    /// <b>Lo que este cambio NO hace.</b> Solo afecta inserciones <b>futuras</b>. Las 1.677 filas de
    /// Ecuador ya mal atribuidas (4,1 M kg) siguen como están: corregirlas es un backfill que tocaría
    /// <b>41 lotes ya liquidados</b>, y esa es una decisión de negocio aparte. El detector
    /// <c>backend/sql/verificar_atribucion_lote_engorde.sql</c> las deja medidas y visibles.
    /// </para>
    /// </summary>
    public partial class FnLoteEngordeDesdeUbicacionExcluyeLiquidados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se conserva la firma de 3 argumentos: la llaman el trigger del histórico y varios
            // scripts de backfill. `CREATE OR REPLACE` cambia el cuerpo sin tocar a ningún llamador.
            // (Agregar un parámetro de fecha con DEFAULT habría creado una SOBRECARGA, no un
            // reemplazo, y las llamadas de 3 argumentos quedarían ambiguas.)
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_lote_ave_engorde_id_desde_ubicacion(
    p_farm_id   integer,
    p_nucleo_id character varying,
    p_galpon_id character varying)
RETURNS integer
LANGUAGE sql
STABLE
AS $function$
    SELECT l.lote_ave_engorde_id
    FROM public.lote_ave_engorde l
    WHERE l.granja_id = p_farm_id
      AND COALESCE(TRIM(l.nucleo_id), '') = COALESCE(TRIM(p_nucleo_id), '')
      AND COALESCE(TRIM(l.galpon_id), '') = COALESCE(TRIM(p_galpon_id), '')
      AND l.deleted_at IS NULL
      -- A9: un lote liquidado esta CONGELADO y no recibe atribucion nueva. Sin esto, un
      -- movimiento cargado hoy puede caer sobre un lote cuya liquidacion ya guardo una copia
      -- de sus numeros, y la copia y el dato vivo dejan de coincidir.
      -- Si no queda ningun lote vivo en el galpon, devuelve NULL: fn_seguimiento_diario_engorde
      -- conserva las filas sin lote (no se pierde alimento) y la apertura del ciclo siguiente
      -- las recoge, que es el mismo camino del alimento previo al encaset.
      AND l.estado_operativo_lote IS DISTINCT FROM 'Cerrado'
    ORDER BY l.lote_ave_engorde_id DESC
    LIMIT 1;
$function$;

COMMENT ON FUNCTION public.fn_lote_ave_engorde_id_desde_ubicacion(integer, character varying, character varying) IS
    'Resuelve el lote de engorde vivo de un galpon. EXCLUYE los liquidados (estado Cerrado): un lote liquidado esta congelado y no recibe atribucion nueva. Devuelve NULL si el galpon no tiene lote vivo; ese caso lo conserva fn_seguimiento_diario_engorde.';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve al comportamiento anterior: el id mas alto del galpon, liquidado o no.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_lote_ave_engorde_id_desde_ubicacion(
    p_farm_id   integer,
    p_nucleo_id character varying,
    p_galpon_id character varying)
RETURNS integer
LANGUAGE sql
STABLE
AS $function$
    SELECT l.lote_ave_engorde_id
    FROM public.lote_ave_engorde l
    WHERE l.granja_id = p_farm_id
      AND COALESCE(TRIM(l.nucleo_id), '') = COALESCE(TRIM(p_nucleo_id), '')
      AND COALESCE(TRIM(l.galpon_id), '') = COALESCE(TRIM(p_galpon_id), '')
      AND l.deleted_at IS NULL
    ORDER BY l.lote_ave_engorde_id DESC
    LIMIT 1;
$function$;
");
        }
    }
}
