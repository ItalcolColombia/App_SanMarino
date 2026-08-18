using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlimentoEntregaCicloEngorde : Migration
    {
        // Plan: fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md (FASE B, §3.2).
        //
        // QUE CREA. `alimento_entrega_ciclo_engorde`: la ATRIBUCION del alimento marcado «para el
        // proximo ciclo», persistida como HECHO (quien entrega, quien recibe, cuantos kg, que dia).
        // Mas sus indices, el indice parcial que le faltaba al historico y los 2 triggers que anulan
        // la entrega cuando se anula o se borra el movimiento origen.
        //
        // POR QUE UNA TABLA Y NO COLUMNAS EN EL HISTORICO.
        //  * La entrega es el UNICO dato de alimento con alcance de LOTE. Todo el resto de la fn
        //    (hist_full, hist_alimento, docs_por_fecha, fechas_universo) filtra por UBICACION y por
        //    diseno no conoce lotes. Meterla ahi obliga a un predicado por lote en 4 CTE que nunca lo
        //    tuvieron.
        //  * El hecho necesita ciclo de vida propio (PENDIENTE -> VIGENTE -> ANULADA, mas `sellada`) y
        //    auditoria propia. `lote_registro_historico_unificado` lo llena un trigger AFTER INSERT
        //    que no propaga ningun UPDATE, y es una tabla de auditoria con 6 indices: no es lugar para
        //    `sellada` ni `anulada_motivo`.
        //  * La INTENCION (`para_proximo_ciclo`, lo que pidio la persona) y el HECHO (lo que el
        //    sistema resolvio) son cosas distintas. Por eso viven separadas y el esquema de la marca
        //    no se toca.
        //
        // POR QUE ES UN HECHO Y NO UN VEREDICTO. La v16 anterior recalculaba la atribucion EN LECTURA
        // sobre estado mutable. La liquidacion congela UN SOLO extremo, asi que al re-leer el otro
        // cambiaba de opinion: liquidar el cedente escondia 3.000 kg reales (apertura del destino
        // 3.000 -> 0, cuadre 0,00 -> -3.000) y liquidar el destino los duplicaba (Sigma galpon
        // 8.640 -> 11.640) con descuadre_kg = 0,00 en los dos estados, o sea con el detector CIEGO.
        // Fue el NO-GO del gate. Escribiendo el hecho una sola vez, congelar un extremo deja de poder
        // cambiar lo que ve el otro: los dos bloqueantes son inconstruibles.
        //
        // SIN EFECTO HASTA QUE ALGUIEN MARQUE. La tabla nace VACIA y la fn todavia no la lee (eso
        // entra con la v16b). Con 0 filas, los indices parciales cuestan ~0. Ademas la marca sigue
        // rechazada por el servidor desde la v16a, asi que nada puede escribirla todavia.
        //
        // Idempotente: CREATE TABLE / CREATE INDEX IF NOT EXISTS, CREATE OR REPLACE FUNCTION y
        // DROP TRIGGER IF EXISTS + CREATE TRIGGER. Se puede aplicar dos veces sin efecto.
        // El ModelSnapshot SI cambia (entidad nueva) — es la unica migracion de esta serie que lo toca.
        // Fuente canonica: backend/sql/create_alimento_entrega_ciclo_engorde.sql
        // El DDL vive en el partial `.Ddl.cs`.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DdlAlimentoEntregaCicloEngorde);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_entrega_ciclo_engorde_mov_cancel ON inventario_gestion_movimiento;
DROP TRIGGER IF EXISTS trg_entrega_ciclo_engorde_mov_del    ON inventario_gestion_movimiento;
DROP FUNCTION IF EXISTS fn_anular_entrega_ciclo_por_movimiento();
DROP TABLE IF EXISTS alimento_entrega_ciclo_engorde;
-- El indice parcial del historico se CONSERVA a proposito: no lo creo esta feature conceptualmente
-- (acelera cualquier lectura por marca) y borrarlo en un Down solo agrega riesgo.
");
        }
    }
}
