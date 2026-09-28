using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Infrastructure.Services;
using ZooSanMarino.Infrastructure.Tests.FlujosValidacion;

namespace ZooSanMarino.Infrastructure.Tests.SeguimientoPostura;

public class MovimientosAlimentoSeguimientoConsultasTests
{
    private static readonly MovimientosAlimentoSeguimientoCalculos.RangoFechas Rango =
        new(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));

    private static ZooSanMarinoContext NuevoContexto()
    {
        var options = new DbContextOptionsBuilder<ZooSanMarinoContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestZooSanMarinoContext(options);
    }

    private static void SembrarEmpresaGranjaEItem(
        ZooSanMarinoContext context,
        bool porSilo,
        bool empresaPorGalpon,
        bool? granjaPorGalpon)
    {
        context.Companies.Add(new Company
        {
            Id = 1,
            Name = "Empresa prueba",
            Identifier = "900-test",
            DocumentType = "NIT",
            ManejaInventarioPorSilo = porSilo,
            ManejaAlimentoPorGalpon = empresaPorGalpon
        });
        context.Farms.Add(new Farm
        {
            Id = 10,
            CompanyId = 1,
            Name = "Granja prueba",
            ManejaAlimentoPorGalpon = granjaPorGalpon
        });
        context.ItemInventario.Add(new ItemInventario
        {
            Id = 100,
            CompanyId = 1,
            PaisId = 1,
            Codigo = "ALI-100",
            Nombre = "Alimento postura",
            TipoItem = "alimento",
            Unidad = "kg"
        });
    }

    private static LoteRegistroHistoricoUnificado Movimiento(
        long id,
        int farmId = 10,
        string? nucleoId = null,
        string? galponId = null,
        int? siloId = null,
        string tipo = "INV_INGRESO",
        decimal cantidad = 125.5m,
        string referencia = "REM-SILO-001") => new()
        {
            Id = id,
            CompanyId = 1,
            FarmId = farmId,
            NucleoId = nucleoId,
            GalponId = galponId,
            SiloId = siloId,
            FechaOperacion = new DateTime(2026, 9, 15),
            TipoEvento = tipo,
            OrigenTabla = "inventario_gestion_movimiento",
            OrigenId = checked((int)id),
            ItemInventarioEcuadorId = 100,
            ItemResumen = "ALI-100 — Alimento postura",
            CantidadKg = cantidad,
            Unidad = "kg",
            Referencia = referencia,
            CreatedAt = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)
        };

    [Fact]
    public async Task ConsultarAsync_PorSilo_MuestraIngresoDelSiloAsignadoConCantidadYReferencia()
    {
        await using var context = NuevoContexto();
        SembrarEmpresaGranjaEItem(context, porSilo: true, empresaPorGalpon: false, granjaPorGalpon: false);
        context.FarmSilos.AddRange(
            new FarmSilo { Id = 1000, CompanyId = 1, GranjaId = 10, Nombre = "Silo 1", Tipo = FarmSilo.TipoSilo },
            new FarmSilo { Id = 1001, CompanyId = 1, GranjaId = 10, Nombre = "Silo 2", Tipo = FarmSilo.TipoSilo });
        context.LoteSilos.Add(new LoteSilo
        {
            Id = 1,
            CompanyId = 1,
            LoteId = 500,
            FarmSiloId = 1000,
            Activo = true
        });
        context.GalponSilos.Add(new GalponSilo
        {
            Id = 1,
            CompanyId = 1,
            GranjaId = 10,
            NucleoId = "N1",
            GalponId = "G1",
            FarmSiloId = 1000,
            Activo = true
        });
        context.LoteRegistroHistoricoUnificados.AddRange(
            Movimiento(1, siloId: 1000),
            Movimiento(2, siloId: 1001, referencia: "REM-OTRO-SILO"));
        await context.SaveChangesAsync();

        var resultado = await MovimientosAlimentoSeguimientoConsultas.ConsultarAsync(
            context, companyId: 1, loteId: 500, farmId: 10,
            nucleoId: "N1", galponId: "G1", Rango);

        var ingreso = Assert.Single(resultado);
        Assert.Equal(125.5m, ingreso.CantidadKg);
        Assert.Equal("REM-SILO-001", ingreso.Referencia);
        Assert.Equal("ALI-100 — Alimento postura", ingreso.Alimento);
        Assert.Equal("INV_INGRESO", ingreso.TipoMovimiento);
    }

    [Fact]
    public async Task ConsultarAsync_PorGalpon_ConservaUbicacionExacta()
    {
        await using var context = NuevoContexto();
        SembrarEmpresaGranjaEItem(context, porSilo: false, empresaPorGalpon: true, granjaPorGalpon: null);
        context.LoteRegistroHistoricoUnificados.AddRange(
            Movimiento(1, nucleoId: "N1", galponId: "G1", referencia: "EXACTO"),
            Movimiento(2, nucleoId: "N1", galponId: "G2", referencia: "OTRO-GALPON"),
            Movimiento(3, referencia: "SIN-GALPON"));
        await context.SaveChangesAsync();

        var resultado = await MovimientosAlimentoSeguimientoConsultas.ConsultarAsync(
            context, companyId: 1, loteId: 500, farmId: 10,
            nucleoId: "N1", galponId: "G1", Rango);

        Assert.Equal("EXACTO", Assert.Single(resultado).Referencia);
    }

    [Fact]
    public async Task ConsultarAsync_PorGranja_NoExigeNucleoNiGalponYPreservaAislamientoDeGranja()
    {
        await using var context = NuevoContexto();
        SembrarEmpresaGranjaEItem(context, porSilo: false, empresaPorGalpon: true, granjaPorGalpon: false);
        context.LoteRegistroHistoricoUnificados.AddRange(
            Movimiento(1, nucleoId: null, galponId: null, referencia: "INGRESO-GRANJA"),
            Movimiento(2, farmId: 99, referencia: "OTRA-GRANJA"));
        await context.SaveChangesAsync();

        var resultado = await MovimientosAlimentoSeguimientoConsultas.ConsultarAsync(
            context, companyId: 1, loteId: 500, farmId: 10,
            nucleoId: "N1", galponId: "G1", Rango);

        Assert.Equal("INGRESO-GRANJA", Assert.Single(resultado).Referencia);
    }
}
