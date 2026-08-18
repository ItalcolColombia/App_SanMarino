using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FnSeguimientoEngordeV16aMarcaInerte : Migration
    {
        // Plan: fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md (FASE A).
        //
        // QUE HACE. La marca `para_proximo_ciclo` vuelve a ser INERTE dentro de la fn diaria de
        // engorde. Se quitan los 5 lugares donde la v15 interpretaba el booleano: el disyunto marcado
        // de `apert_mov` y los 4 guards de `hist_full`, `hist_alimento`, `docs_por_fecha` y
        // `fechas_universo`. El filtro vuelve a ser el de la v14 exacto. La columna, su trigger, el
        // endpoint y el badge del front NO se tocan: se apaga la interpretacion, no el dato.
        //
        // POR QUE. La v15 quita kg de una pantalla y ESPERA que otra los muestre. Los 4 guards
        // filtran por UBICACION (granja+nucleo+galpon) y le quitan el movimiento a TODO lote con
        // seguimiento, incluidos los que CONVIVEN con el ciclo destino; en esos galpones ninguna
        // apertura lo vuelve a tomar y los kilos no aparecen en ningun lado. Medido sobre la BD local
        // con dump tipo prod, marcando los 2.371 movimientos de alimento reales (todo en transaccion
        // con ROLLBACK):
        //
        //                                   v15 (lo desplegado)      v16a (esta migracion)
        //   filas de la diaria que se caen   24 (3 EC + 21 PA)        0
        //   filas con saldo distinto         1.733 (peor 193.701,7)   0
        //   filas en negativo                97 -> 1.160              97 -> 97
        //   galpones descuadrados            8 -> 58                  8 -> 8
        //
        // Notese que Panama sale PEOR que Ecuador (21 filas perdidas contra 3): es donde viven los
        // ciclos que conviven, que es justo lo que los guards rompen. Un fix medido en un solo pais
        // no lo habria visto.
        //
        // El rediseno por ENTREGA (v16, 08-ago) tampoco cerro: recalculaba la atribucion EN LECTURA
        // sobre estado mutable, y la liquidacion congela un solo extremo, asi que el handoff se
        // partia. Fue NO-GO del gate y se revirtio sin llegar a un commit. La atribucion vuelve en la
        // Fase B como HECHO PERSISTIDO que la fn LEE. Esta migracion es el piso limpio para eso.
        //
        // GATE MULTIPAIS (CLAUDE.md), backend/sql/verificar_paridad_saldo_engorde.sql, antes y
        // despues, sin flags:
        //   - 6.429 filas en las dos corridas (ItalcolEcuador 5.296 + ItalcolPanama 1.133);
        //   - 0 filas que desaparecen, 0 filas nuevas, 0 dif_saldo_alimento, 0 dif_saldo_aves,
        //     0 dif_ingreso, 0 dif_consumo y 0 dif_documento EN LAS DOS EMPRESAS;
        //   - 6.342 filas de seguimiento esperadas == 6.342 presentes;
        //   - fn_cuadre_alimento_engorde(NULL): 67 filas / 8 descuadrados antes y despues.
        // Y con la marca PRENDIDA en los 2.371 movimientos: EXCEPT ALL bidireccional 0 y 0 sobre las
        // 6.429 filas, con apertura_alimento_kg y apertura_documentos incluidas.
        //
        // Idempotente: el script empieza con DROP FUNCTION IF EXISTS + CREATE. Sin DDL de tablas ni
        // cambios de modelo (ModelSnapshot intacto). La firma no cambia respecto de la v15 (siguen
        // las 49 columnas OUT) y ningun objeto depende de la fn (`pg_depend` vacio): los 5
        // consumidores la llaman por CROSS JOIN LATERAL con columnas NOMBRADAS.
        // Fuente canonica: backend/sql/fn_seguimiento_diario_engorde.sql
        // Las constantes SQL viven en el partial `.Fn.cs`.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV16a);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV15);
        }
    }
}
