using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cierra los <b>13 casos</b> de Agroavicola Sanmarino, ItalcolPanama e ItalcolEcuador cuyo
    /// arreglo está verificado en el código y desplegado en producción, pero que seguían abiertos en
    /// el tablero: <b>11 en <c>SOLUCIONADO</c></b> y <b>2 en <c>EN_ANALISIS</c></b>.
    /// </summary>
    /// <remarks>
    /// <b>Por qué seguían abiertos.</b> A <c>CERRADO</c> solo se llega por
    /// <c>TicketService.ConfirmarCierreAsync</c>, que exige ser el solicitante — es ceremonia de
    /// diseño, no un olvido. Los 11 solucionados llevaban de <b>3 a 25 días</b> esperando esa
    /// confirmación. Tres de ellos (<c>TK-2026-000020</c>, <c>-000164</c>, <c>-000165</c>) se habían
    /// marcado <c>SOLUCIONADO</c> por migración: quedaron <b>sin nota de estado y con
    /// <c>notificado_correo = false</c></b>, así que su solicitante nunca supo que el caso estaba
    /// resuelto y difícilmente iba a cerrarlo. Los 2 en <c>EN_ANALISIS</c> estaban resueltos y nadie
    /// movió la tarjeta.
    ///
    /// <b>Qué se verificó antes de cerrar</b> (cada uno contra su artefacto, y confirmando que el
    /// commit es ancestro de <c>origin/main-produccion</c>): el campo «Fecha del movimiento» del
    /// modal de movimientos (12), <c>tipo_alimento</c> ampliado de <c>varchar(100)</c> a
    /// <c>varchar(500)</c> (13 y 14), <c>7339c61</c> —el lote sin cerrar que absorbía el ciclo
    /// siguiente— (15), el no-bug documentado con la medición en producción (20), cero grupos de
    /// ingresos duplicados hoy en Panamá (163), <c>b355f71</c> —la doble validación separa y
    /// descuenta solo al validar— (164), <c>ValidacionSeguimientoCalculos.Canonico</c> vivo y cero
    /// referencias a la tabla inexistente (165), el disponible de inventario que ya descuenta las
    /// reservas activas con el silo en la clave (166), <c>299c816</c> —las grillas mostraban el saldo
    /// bajo el rótulo «aves encasetadas»— (176), <c>a9fd721</c> + <c>3988183</c> (177), <c>c13b9ef</c>
    /// (185) y <c>1191b39</c> —sin día cero en indicadores— (187).
    ///
    /// <b>Los 2 que NO entran, a propósito.</b> <c>TK-2026-000183</c> (CAROLINA) tiene trabajo real
    /// pendiente: el diagnóstico está completo pero los datos no se corrigieron —es una decisión de
    /// negocio, exige reabrir un lote cerrado— y el mecanismo que lo produce sigue vivo en
    /// <c>InventarioGestionService.StockMutacion.cs:118-145</c>. <c>TK-2026-000001</c> («pruebas
    /// Moises», 30-jun) es un caso de prueba, no una solicitud.
    ///
    /// <b>Localiza por <c>codigo</c>, no por título.</b> Al revés que el caso de Santa Reyes, estos
    /// los creó la aplicación y no un seed: su <c>codigo</c> (<c>TK-2026-NNNNNN</c>, derivado del id)
    /// es el identificador de negocio estable y visible, mientras que varios títulos los tipeó el
    /// usuario (<c>ERROR EN LA FEHCA</c>) y comparar texto libre sería frágil. Se cruza además contra
    /// el <b>nombre de la empresa</b> esperada, nunca contra su id.
    ///
    /// <b>Fail-safe por estado, además de idempotente.</b> Cada caso declara el estado en el que se
    /// lo espera; si en producción ya está <c>CERRADO</c>, o alguien lo reabrió a otro estado, se
    /// <b>saltea con <c>RAISE NOTICE</c></b> en vez de forzarlo. Cerrar a ciegas un caso que el
    /// solicitante reabrió sería peor que no cerrarlo.
    ///
    /// <b>La nota de cierre dice quién cerró y por qué.</b> Deja escrito que el cierre lo hizo la
    /// gestión y no el solicitante, cuántos días esperó la confirmación, qué evidencia se verificó y
    /// que el caso se reabre —o se registra uno nuevo— si el problema vuelve. A los 3 sin nota de
    /// <c>SOLUCIONADO</c> se les siembra también esa nota, fechada en su <c>fecha_solucion</c> real,
    /// reparando el hueco de línea de tiempo que dejó haberlos marcado por SQL.
    ///
    /// Plan: <c>fase_de_desarrollo/cierre_tickets_resueltos_otras_empresas_plan.md</c>.
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto. El SQL vive en el partial
    /// <c>.Seed.cs</c> por tamaño.
    /// </remarks>
    public partial class CerrarTicketsResueltosOtrasEmpresas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CIERRE_SQL);
        }

        /// <summary>
        /// Devuelve cada caso a su estado previo (<c>SOLUCIONADO</c> los 11, <c>EN_ANALISIS</c> los
        /// 2), limpia solo lo que escribió el <c>Up</c> —comparando contra sus propios valores, para
        /// no borrar una fecha que ya venía de antes— y borra las notas que sembró.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(REVERT_SQL);
        }
    }
}
