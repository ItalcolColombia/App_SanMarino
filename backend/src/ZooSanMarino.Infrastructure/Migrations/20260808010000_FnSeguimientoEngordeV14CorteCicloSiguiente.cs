using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FnSeguimientoEngordeV14CorteCicloSiguiente : Migration
    {
        // Ticket de operacion (Ecuador, 07-ago-2026): «granja KM 86 lote 01 galpon 1 y 02: tenemos
        // ingreso del mes de julio cuando el lote cerro en abril».
        //
        // El lote 2601 de Kilometro 86 / Galpon-1 tiene su ultimo seguimiento el 2026-04-20 y la grilla
        // llegaba hasta el 2026-08-03, con el saldo de alimento inflado de 1.600 kg a 206.450 kg. Los
        // ingresos de julio son CORRECTOS: son del lote 2603, encasetado en ese mismo galpon el 24/06.
        // El error era a que lote se los mostraba.
        //
        // Causa: `fecha_max` de la fn solo se cierra por saldo 0 (`saldo_close`, v5) o por
        // `estado_operativo_lote = 'cerrado'`. Ese lote nunca se liquido, y su saldo NUNCA llega a 0
        // justamente porque el galpon siguio recibiendo alimento para los ciclos siguientes ⇒
        // `fecha_max = NULL` ⇒ grilla sin tope superior. Y `fechas_universo` / `hist_alimento` /
        // `docs_por_fecha` filtran por UBICACION (granja+nucleo+galpon), no por lote.
        //
        // Fix: CTE `corte_ciclo_siguiente`, complemento exacto de `corte_apertura` (v12). El galpon
        // deja de ser mio el dia en que OTRO lote del mismo granja+nucleo+galpon empieza a tener
        // seguimiento despues de que yo deje de tenerlo. Criterio ESTRUCTURAL, no numerico: no depende
        // de que alguien se acuerde de liquidar el lote.
        //
        // Gate multipais (CLAUDE.md) corrido sobre el dump de produccion, antes y despues:
        //   - de 140 lotes solo cambian 2, los dos de Ecuador (lote 2: 31 filas; lote 86: 1 fila);
        //   - 0 diferencias de saldo / aves / ingreso / consumo / documento en las filas que quedan;
        //   - ItalcolPanama NO-OP exacto;
        //   - 0 filas con seguimiento real perdidas (solo desaparecen filas movimiento-only);
        //   - fn_cuadre_alimento_engorde 22 -> 22 y fn_cuadre_aves_engorde 1 -> 1 (sin regresion).
        //
        // Idempotente: CREATE OR REPLACE con la MISMA firma, sin DDL de tablas ni cambios de modelo
        // (ModelSnapshot intacto). Fuente canonica: backend/sql/fn_seguimiento_diario_engorde.sql
        // Las constantes SQL viven en el partial `.Fn.cs`.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV14);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV13);
        }
    }
}
