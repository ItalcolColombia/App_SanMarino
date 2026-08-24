// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoService.Alimento.cs
// Alimento consumido por galpon/fecha: inventario unificado, ingresos y traslados del dia.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoService
{
    /// <summary>
    /// ¿La empresa DUEÑA de la granja declaró que sus reportes leen el inventario unificado?
    /// Se resuelve por la granja (no por el token) para que el reporte no dependa de con qué empresa
    /// esté logueado quien lo abre. Fail-closed: sin dato, la tabla de siempre.
    /// </summary>
    private async Task<bool> LeeInventarioUnificadoAsync(int granjaId, CancellationToken ct)
    {
        var flag = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId)
            .Join(_ctx.Companies.AsNoTracking(), f => f.CompanyId, c => c.Id,
                (_, c) => c.ReportesAlimentoDesdeInventarioUnificado)
            .FirstOrDefaultAsync(ct);

        return ReporteAlimentoInventarioCalculos.LeeInventarioUnificado(flag);
    }

    /// <summary>
    /// Kilos de alimento del día en <c>inventario_gestion_movimiento</c> para los tipos indicados.
    /// El tipo de ítem se resuelve contra el catálogo del módulo nuevo
    /// (<c>item_inventario_ecuador</c>), no por el nombre del producto: el filtro viejo buscaba la
    /// palabra «alimento» en el nombre y se perdía todo lo que no la tuviera.
    /// </summary>
    private async Task<decimal> SumaAlimentoUnificadoAsync(int granjaId, DateTime fecha, string[] tipos, CancellationToken ct)
    {
        var desde = new DateTimeOffset(DateTime.SpecifyKind(fecha.Date, DateTimeKind.Utc));
        var hasta = desde.AddDays(1);

        return await _ctx.InventarioGestionMovimientos
            .AsNoTracking()
            .Where(m => m.FarmId == granjaId && m.CreatedAt >= desde && m.CreatedAt < hasta)
            .Where(m => tipos.Contains(m.MovementType))
            .Join(_ctx.ItemInventario.AsNoTracking(),
                m => m.ItemInventarioEcuadorId,
                i => i.Id,
                (m, i) => new { m, i })
            .Where(x => x.i.TipoItem.Trim().ToLower() == "alimento")
            .SumAsync(x => x.m.Quantity, ct);
    }

    private decimal CalcularBultos(decimal kilos)
    {
        // Asumiendo que un bulto estándar pesa 40kg
        const decimal pesoBulto = 40m;
        return kilos / pesoBulto;
    }

    private async Task<decimal> ObtenerIngresosAlimentoAsync(int granjaId, DateTime fecha, CancellationToken ct)
    {
        // Obtener ingresos de alimentos (Entry, TransferIn) del día
        // Filtrar por nombre que contenga "alimento" o códigos comunes de alimentos
        try
        {
            // Empresas sobre el módulo unificado: su alimento no está en la tabla vieja, así que sin
            // este desvío el reporte devolvería 0 — y el catch de abajo lo haría en silencio.
            if (await LeeInventarioUnificadoAsync(granjaId, ct))
                return await SumaAlimentoUnificadoAsync(granjaId, fecha, ReporteAlimentoInventarioCalculos.TiposEntrada, ct);

            var ingresos = await _ctx.FarmInventoryMovements
                .AsNoTracking()
                .Include(m => m.CatalogItem)
                .Where(m => m.FarmId == granjaId &&
                           m.CreatedAt.Date == fecha.Date &&
                           (m.MovementType == Domain.Enums.InventoryMovementType.Entry ||
                            m.MovementType == Domain.Enums.InventoryMovementType.TransferIn) &&
                           m.CatalogItem != null &&
                           (m.CatalogItem.Nombre.ToLower().Contains("alimento") ||
                            m.CatalogItem.Nombre.ToLower().Contains("food") ||
                            (m.CatalogItem.Codigo != null && m.CatalogItem.Codigo.ToLower().StartsWith("al"))))
                .SumAsync(m => m.Quantity, ct);

            return ingresos;
        }
        catch
        {
            return 0; // Si hay error, retornar 0
        }
    }

    private async Task<decimal> ObtenerTrasladosAlimentoAsync(int granjaId, DateTime fecha, CancellationToken ct)
    {
        // Obtener traslados de alimentos (TransferOut) del día
        try
        {
            if (await LeeInventarioUnificadoAsync(granjaId, ct))
                return await SumaAlimentoUnificadoAsync(granjaId, fecha, ReporteAlimentoInventarioCalculos.TiposTraslado, ct);

            var traslados = await _ctx.FarmInventoryMovements
                .AsNoTracking()
                .Include(m => m.CatalogItem)
                .Where(m => m.FarmId == granjaId &&
                           m.CreatedAt.Date == fecha.Date &&
                           m.MovementType == Domain.Enums.InventoryMovementType.TransferOut &&
                           m.CatalogItem != null &&
                           (m.CatalogItem.Nombre.ToLower().Contains("alimento") ||
                            m.CatalogItem.Nombre.ToLower().Contains("food") ||
                            (m.CatalogItem.Codigo != null && m.CatalogItem.Codigo.ToLower().StartsWith("al"))))
                .SumAsync(m => m.Quantity, ct);

            return traslados;
        }
        catch
        {
            return 0; // Si hay error, retornar 0
        }
    }
}
