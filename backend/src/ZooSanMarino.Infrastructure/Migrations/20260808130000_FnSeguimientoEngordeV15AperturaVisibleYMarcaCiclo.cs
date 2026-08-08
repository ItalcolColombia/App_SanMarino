using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FnSeguimientoEngordeV15AperturaVisibleYMarcaCiclo : Migration
    {
        // Plan: fase_de_desarrollo/ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md (D1 y D2).
        //
        // Pedido de operacion: el alimento llega a la granja 2-7 dias ANTES del encasetamiento, pero
        // hay que decirle a cada persona que lo registre con la fecha del PRIMER DIA DE CONSUMO para
        // que la tabla diaria "cuadre". Asi se pierde la fecha real que necesita contabilidad. Medido
        // sobre el dump: Ecuador aplica ese workaround en 110 de 110 ciclos; Panama ya fecha real en 9
        // de 30.
        //
        // (A) APERTURA VISIBLE. El alimento previo al encaset YA entraba al saldo del dia 1 desde la
        //     v9 (CTE `apertura_alimento`), pero era un escalar interno: `ingreso_alimento_kg` mostraba
        //     0 y `documento` vacio, asi que el saldo "aparecia de la nada". Esta migracion agrega dos
        //     columnas ADITIVAS al final del RETURNS TABLE -`apertura_alimento_kg` y
        //     `apertura_documentos`- pobladas SOLO en la primera fila del ciclo. El saldo, el conteo de
        //     filas y todas las columnas previas quedan intactos: es exposicion, no calculo nuevo.
        //
        // (B) OVERRIDE POR MARCA `para_proximo_ciclo` (columna creada por la migracion 20260808120000):
        //     un movimiento marcado entra a la apertura del ciclo siguiente aunque caiga fuera de la
        //     ventana `corte_apertura` (v12) o lo atribuya `lote_ave_engorde_id` a un lote ajeno (v11),
        //     y deja de figurar como fila/columna diaria del ciclo cuyo rango lo contenga. Es la unica
        //     atribucion posible en los galpones ENCADENADOS de Ecuador (28 de 75 ciclos encadenados
        //     tienen menos de 10 dias entre el fin del ciclo previo y su encaset). Sin marca -que es
        //     todo lo que existe hoy: la columna nace en `false`- el resultado es el de la v14.
        //
        // ⚠️ La firma CAMBIA (dos columnas OUT nuevas) => `CREATE OR REPLACE` NO alcanza: PostgreSQL
        // responde «cannot change return type of existing function». Por eso el Up() y el Down()
        // empiezan con DROP FUNCTION. Verificado que ningun objeto depende de la fn (`pg_depend` sin
        // filas): los consumidores la invocan por CROSS JOIN LATERAL con columnas NOMBRADAS
        // (fn_reporte_diario_costos_engorde, fn_informe_semanal_pollo_engorde,
        // fn_cuadre_alimento_engorde, verificar_paridad_saldo_engorde) o por SELECT * mapeado por
        // nombre (SeguimientoAvesEngordeEcuadorService.Consultas), y vw_seguimiento_pollo_engorde es
        // una reimplementacion set-based que no la llama.
        //
        // Gate multipais (CLAUDE.md) corrido sobre la BD local con dump tipo prod, antes y despues:
        //   - 5.804 filas / 147 lotes en las dos corridas, mismo conteo;
        //   - 0 filas perdidas, 0 filas nuevas y 0 filas distintas en las 46 columnas preexistentes,
        //     tanto en ItalcolEcuador como en ItalcolPanama;
        //   - fn_cuadre_alimento_engorde 1 -> 1 descuadrado y fn_cuadre_aves_engorde 1 -> 1 (identicas
        //     fila a fila); fn_reporte_diario_costos_engorde 224 filas byte a byte;
        //   - apertura visible: 9 ciclos de Panama con apertura positiva (70.030,369 kg; DAYLAND G0464
        //     = 16.137,621 kg con documentos "LLEG-01, LLEG-02") y 2 de Ecuador (7.200 kg).
        //
        // Idempotente: DROP FUNCTION IF EXISTS + CREATE. Sin DDL de tablas ni cambios de modelo
        // (ModelSnapshot intacto). Fuente canonica: backend/sql/fn_seguimiento_diario_engorde.sql
        // Las constantes SQL viven en el partial `.Fn.cs`.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volver de 48 a 46 columnas OUT tambien cambia el tipo de retorno: hay que dropear antes.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde(INT);");
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV14);
        }
    }
}
