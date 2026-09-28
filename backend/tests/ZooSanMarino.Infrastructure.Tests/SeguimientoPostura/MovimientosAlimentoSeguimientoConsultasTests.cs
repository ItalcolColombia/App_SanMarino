using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.Infrastructure.Tests.SeguimientoPostura;

public class MovimientosAlimentoSeguimientoConsultasTests
{
    [Fact]
    public void MapearFila_ConservaMovimientosClasificadosYReferenciasDeLaFuncionSql()
    {
        var fila = new MovimientosAlimentoSeguimientoConsultas.MovimientoAlimentoDiaSqlRow
        {
            Fecha = new DateTime(2026, 9, 15),
            IngresosJson = """
                [{"id":1,"fecha":"2026-09-15","tipoMovimiento":"INV_INGRESO","cantidadKg":125.5,"alimento":"ALI-100 — Postura","referencia":"REM-001","numeroDocumento":null}]
                """,
            TrasladosJson = """
                [{"id":2,"fecha":"2026-09-15","tipoMovimiento":"INV_TRASLADO_ENTRADA","cantidadKg":25,"alimento":"ALI-100 — Postura","referencia":null,"numeroDocumento":"TR-002"},{"id":3,"fecha":"2026-09-15","tipoMovimiento":"INV_TRASLADO_SALIDA","cantidadKg":10,"alimento":"ALI-100 — Postura","referencia":"TR-003","numeroDocumento":null}]
                """,
            ReferenciasJson = "[\"REM-001\",\"TR-002\",\"TR-003\"]"
        };

        var resultado = MovimientosAlimentoSeguimientoConsultas.MapearFila(fila);

        Assert.Equal(new DateTime(2026, 9, 15), resultado.Fecha);
        Assert.Equal(125.5m, Assert.Single(resultado.Ingresos).CantidadKg);
        Assert.Equal(
            new[] { "INV_TRASLADO_ENTRADA", "INV_TRASLADO_SALIDA" },
            resultado.Traslados.Select(m => m.TipoMovimiento));
        Assert.Equal(new[] { "REM-001", "TR-002", "TR-003" }, resultado.Referencias);
    }

    [Fact]
    public void MapearFila_RespetaColeccionesVaciasQueEntregaPostgresql()
    {
        var resultado = MovimientosAlimentoSeguimientoConsultas.MapearFila(new()
        {
            Fecha = new DateTime(2026, 9, 16),
            IngresosJson = "[]",
            TrasladosJson = "[]",
            ReferenciasJson = "[]"
        });

        Assert.Empty(resultado.Ingresos);
        Assert.Empty(resultado.Traslados);
        Assert.Empty(resultado.Referencias);
    }
}
