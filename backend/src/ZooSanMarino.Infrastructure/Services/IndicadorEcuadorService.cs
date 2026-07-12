// Partial 'ancla' del servicio de indicadores Ecuador: usings, campos, ctor, constantes,
// helpers compartidos (conversión ajustada pura, metros cuadrados del galpón) y la interfaz.
// La implementación vive repartida por responsabilidad en 'IndicadorEcuador/Funciones/':
//   - Indicadores      → indicadores de lotes levante/producción (universo `lotes`).
//   - Consolidado      → agregación consolidada de todas las granjas.
//   - LiquidacionPeriodo → liquidación por período y lotes cerrados/en rango.
//   - PolloEngorde     → liquidación técnica pollo engorde (Lote Ave Engorde + reproductores).
// Namespace plano → misma DI, misma interfaz, mismo comportamiento. La aritmética pura vive en
// Application/Calculos/IndicadorEcuadorCalculos.cs.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class IndicadorEcuadorService : IIndicadorEcuadorService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    // Variables configurables para conversión ajustada
    private const decimal PesoAjusteDefault = 2.7m;
    private const decimal DivisorAjusteDefault = 4.5m;

    // Convierte TipoFiltroLotes en los dos booleanos que consume CalcularIndicadorLoteAveEngordeAsync.
    private static (bool soloLotesCerrados, bool usarCierreAdministrativo) ResolveFiltroLotes(string? tipo) =>
        (tipo ?? "cerrados") switch
        {
            "todos"    => (false, false),
            "aves_cero" => (true,  false),
            _          => (true,  true)   // "cerrados" y cualquier otro valor
        };

    public IndicadorEcuadorService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // ─── Delegador thin a la aritmética pura (Application/Calculos) ──────────────
    private static decimal CalcularConversionAjustada(decimal conversion, decimal pesoPromedio, decimal pesoAjuste, decimal divisorAjuste)
        => IndicadorEcuadorCalculos.ConversionAjustada(conversion, pesoPromedio, pesoAjuste, divisorAjuste);

    // ─── Helper compartido (levante/producción y pollo engorde reproductora) ────
    private async Task<decimal> CalcularMetrosCuadradosAsync(string? galponId, int granjaId)
    {
        if (string.IsNullOrEmpty(galponId))
        {
            // Si no hay galpón específico, sumar área de todos los galpones de la granja
            var galpones = await _context.Galpones
                .AsNoTracking()
                .Where(g => g.GranjaId == granjaId && g.DeletedAt == null)
                .ToListAsync();

            decimal totalArea = 0;
            foreach (var galpon in galpones)
            {
                if (decimal.TryParse(galpon.Ancho, out var ancho) &&
                    decimal.TryParse(galpon.Largo, out var largo))
                {
                    totalArea += ancho * largo;
                }
            }
            return totalArea;
        }
        else
        {
            var galpon = await _context.Galpones
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GalponId == galponId && g.GranjaId == granjaId);

            if (galpon != null &&
                decimal.TryParse(galpon.Ancho, out var ancho) &&
                decimal.TryParse(galpon.Largo, out var largo))
            {
                return ancho * largo;
            }
        }

        return 0;
    }

    private class LoteInfo
    {
        public int LoteId { get; set; }
        public string LoteNombre { get; set; } = "";
        public int GranjaId { get; set; }
        public string GranjaNombre { get; set; } = "";
        public string? GalponId { get; set; }
        public string GalponNombre { get; set; } = "";
        public DateTime? FechaEncaset { get; set; }
    }
}
