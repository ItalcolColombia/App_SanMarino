// src/ZooSanMarino.Infrastructure/Services/CuadreAlimentoEngordeService.cs
// Lee el invariante del alimento de engorde desde fn_cuadre_alimento_engorde y lo clasifica con
// CuadreAlimentoEngordeCalculos (puro y testeado).
//
// El descuadre de jul-2026 lo detectó un humano de operación semanas después de producirse: nada en el
// sistema lo verificaba. Este service es lo que faltaba.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class CuadreAlimentoEngordeService : ICuadreAlimentoEngordeService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;
    private readonly ICompanyResolver _companyResolver;

    public CuadreAlimentoEngordeService(
        ZooSanMarinoContext db, ICurrentUser? current, ICompanyResolver companyResolver)
    {
        _db = db;
        _current = current;
        _companyResolver = companyResolver;
    }

    /// <summary>Proyección cruda de la función SQL. Nombres en snake_case, como exige EF con SqlQueryRaw.</summary>
    private sealed class FilaCruda
    {
        public int      company_id            { get; set; }
        public string   empresa               { get; set; } = "";
        public int      granja_id             { get; set; }
        public string   granja                { get; set; } = "";
        public string   nucleo_id             { get; set; } = "";
        public string   galpon_id             { get; set; } = "";
        public int      lote_ave_engorde_id   { get; set; }
        public string   lote_nombre           { get; set; } = "";
        public string   estado_operativo_lote { get; set; } = "";
        public DateTime ultimo_seguimiento    { get; set; }
        public double   saldo_tabla_kg        { get; set; }
        public double   mov_post_kg           { get; set; }
        public double   stock_kg              { get; set; }
        public double   esperado_kg           { get; set; }
        public double   descuadre_kg          { get; set; }
        public int      filas_negativas       { get; set; }
    }

    /// <summary>
    /// Empresa efectiva: la activa del contexto manda sobre el claim, igual que
    /// <c>InventarioGestionService.GetEffectiveCompanyIdAsync</c>.
    /// <para>
    /// Fail-closed: devuelve 0 si no se puede resolver. Un cuadre que mezclara empresas sería peor que
    /// no tenerlo (mismo criterio que <c>InventarioCatalogoScopeCalculos</c>).
    /// </para>
    /// </summary>
    private async Task<int> ResolverCompanyIdAsync()
    {
        int companyId = 0;
        if (_current is not null)
        {
            if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
            {
                var porNombre = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
                if (porNombre.HasValue) companyId = porNombre.Value;
            }
            if (companyId <= 0 && _current.CompanyId > 0)
                companyId = _current.CompanyId;
        }
        return companyId;
    }

    public async Task<CuadreAlimentoEngordeDto> ObtenerAsync(
        bool soloConProblemas = false, CancellationToken ct = default)
    {
        var companyId = await ResolverCompanyIdAsync();
        if (companyId <= 0)
            return new CuadreAlimentoEngordeDto(0, 0, 0, 0, 0m, []);

        var crudas = await _db.Database
            .SqlQueryRaw<FilaCruda>("SELECT * FROM fn_cuadre_alimento_engorde({0}::int)", companyId)
            .ToListAsync(ct);

        // Kilos SEPARADOS y todavia sin aplicar, por ubicacion. Con doble validacion el consumo de un
        // registro pendiente ya esta dentro de `saldo_tabla` (ninguna fn mira `validado`) pero todavia
        // no salio del inventario: sin restarlo del stock, cada pendiente se reportaba como descuadre.
        // Con el flag apagado no hay reservas ACTIVAS y este diccionario queda vacio.
        var reservado = (await _db.SeguimientoReservaAlimento.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.Estado == EstadoReservaSeguimiento.Activa)
                .GroupBy(x => new { x.FarmId, x.NucleoId, x.GalponId })
                .Select(g => new { g.Key.FarmId, g.Key.NucleoId, g.Key.GalponId, Kg = g.Sum(x => x.CantidadKg) })
                .ToListAsync(ct))
            .ToDictionary(
                x => (x.FarmId, (x.NucleoId ?? "").Trim(), (x.GalponId ?? "").Trim()),
                x => x.Kg);

        var filas = crudas.Select(r =>
        {
            reservado.TryGetValue((r.granja_id, (r.nucleo_id ?? "").Trim(), (r.galpon_id ?? "").Trim()),
                out var reservadoKg);
            var descuadre = CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas(
                (decimal)r.descuadre_kg, reservadoKg);
            return new CuadreAlimentoEngordeFilaDto(
                r.company_id, r.empresa, r.granja_id, r.granja, r.nucleo_id, r.galpon_id,
                r.lote_ave_engorde_id, r.lote_nombre, r.estado_operativo_lote, r.ultimo_seguimiento,
                (decimal)r.saldo_tabla_kg, (decimal)r.mov_post_kg, (decimal)r.stock_kg,
                (decimal)r.esperado_kg, descuadre, r.filas_negativas,
                CuadreAlimentoEngordeCalculos.Clasificar(descuadre, r.filas_negativas),
                CuadreAlimentoEngordeCalculos.Describir(descuadre, r.filas_negativas));
        }).ToList();

        // El resumen se calcula SIEMPRE sobre el total; el filtro solo recorta el detalle, para que
        // «0 descuadrados de 35» siga siendo legible aunque se pida solo lo problemático.
        var resumen = new CuadreAlimentoEngordeDto(
            TotalGalpones:    filas.Count,
            Cuadran:          filas.Count(f => f.Estado == EstadoCuadreAlimento.Ok),
            Descuadrados:     filas.Count(f => f.Estado == EstadoCuadreAlimento.Descuadrado),
            ConSaldoNegativo: filas.Count(f => f.Estado == EstadoCuadreAlimento.SaldoNegativo),
            KgErrorAbsoluto:  filas.Sum(f => Math.Abs(f.DescuadreKg)),
            Galpones:         soloConProblemas
                                  ? filas.Where(f => f.Estado != EstadoCuadreAlimento.Ok).ToList()
                                  : filas);

        return resumen;
    }
}
