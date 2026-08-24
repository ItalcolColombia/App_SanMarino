// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoService.Sublotes.cs
// Resolucion de sublotes de un lote base y lectura de sus seguimientos de levante (tabla unificada).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoService
{
    public async Task<List<string>> ObtenerSublotesAsync(string loteNombreBase, int? loteId = null, CancellationToken ct = default)
    {
        List<Lote> lotes;
        
        // Si se proporciona loteId, usar lógica de lote padre
        if (loteId.HasValue)
        {
            var loteSeleccionado = await _ctx.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteId.Value && 
                                         l.CompanyId == _currentUser.CompanyId &&
                                         l.DeletedAt == null, ct);
            
            if (loteSeleccionado == null)
                return new List<string>();
            
            // Si el lote seleccionado es un lote padre, traer todos sus hijos
            if (loteSeleccionado.LotePadreId == null)
            {
                // Es un lote padre, traer todos los lotes que tienen este como padre
                lotes = await _ctx.Lotes
                    .AsNoTracking()
                    .Where(l => l.LotePadreId == loteId.Value &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
                
                // Incluir también el lote padre
                lotes.Insert(0, loteSeleccionado);
            }
            else
            {
                // Es un lote hijo, traer el padre y todos sus hermanos (incluyendo el seleccionado)
                var padreId = loteSeleccionado.LotePadreId.Value;
                lotes = await _ctx.Lotes
                    .AsNoTracking()
                    .Where(l => (l.LotePadreId == padreId || l.LoteId == padreId) &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
            }
        }
        else
        {
            // Lógica antigua: buscar por nombre base (compatibilidad hacia atrás)
            lotes = await _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.LoteNombre.StartsWith(loteNombreBase) &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);
        }

        // Extraer los nombres de sublotes
        var sublotes = lotes
            .Select(l => ExtraerSublote(l.LoteNombre) ?? "Sin sublote")
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        return sublotes;
    }

    /// <summary>
    /// Obtiene todos los sublotes de un lote base levante (busca padre e hijos).
    /// Soporta lógica de lote padre: si es padre trae hijos, si es hijo trae padre + hermanos.
    /// </summary>
    private async Task<List<LotePosturaLevante>> ObtenerSublotesLevantePorLoteBaseAsync(
        int lotePosturaLevanteId,
        CancellationToken ct)
    {
        var loteSeleccionado = await _ctx.LotePosturaLevante
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId &&
                                     l.CompanyId == _currentUser.CompanyId &&
                                     l.DeletedAt == null, ct);

        if (loteSeleccionado == null)
            return new List<LotePosturaLevante>();

        List<LotePosturaLevante> sublotes;

        // Si el lote seleccionado es un lote padre (LotePosturaLevantePadreId == null)
        if (loteSeleccionado.LotePosturaLevantePadreId == null)
        {
            // Traer todos los lotes que tienen este como padre
            sublotes = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Where(l => l.LotePosturaLevantePadreId == lotePosturaLevanteId &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);

            // Incluir el lote padre siempre
            sublotes.Insert(0, loteSeleccionado);

            // Fallback: si no hay hijos vinculados por FK, buscar por prefijo de nombre
            if (sublotes.Count == 1)
            {
                var nombreBase = ExtraerNombreBase(loteSeleccionado.LoteNombre);
                var porNombre = await _ctx.LotePosturaLevante
                    .AsNoTracking()
                    .Where(l => l.LotePosturaLevanteId != lotePosturaLevanteId &&
                               l.LoteNombre.StartsWith(nombreBase) &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);

                if (porNombre.Count > 0)
                    sublotes.AddRange(porNombre);
            }
        }
        else
        {
            // Es un lote hijo: traer el padre y todos sus hermanos
            var padreId = loteSeleccionado.LotePosturaLevantePadreId.Value;
            sublotes = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Where(l => (l.LotePosturaLevantePadreId == padreId || l.LotePosturaLevanteId == padreId) &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);

            // Fallback por nombre si no se encontraron hermanos vía FK
            if (sublotes.Count <= 1)
            {
                var nombreBase = ExtraerNombreBase(loteSeleccionado.LoteNombre);
                sublotes = await _ctx.LotePosturaLevante
                    .AsNoTracking()
                    .Where(l => l.LoteNombre.StartsWith(nombreBase) &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
            }
        }

        return sublotes;
    }

    private static string ExtraerNombreBase(string loteNombre)
    {
        var partes = loteNombre.Trim().Split(' ');
        if (partes.Length > 1 && partes[^1].Length <= 2)
            return string.Join(' ', partes[..^1]);
        return loteNombre.Trim();
    }

    /// <summary>Registro de seguimiento levante leído desde la tabla unificada seguimiento_diario (TipoSeguimiento = levante).</summary>
    private sealed class SegLevanteParaReporte
    {
        public int Id { get; set; }
        public int LoteId { get; set; }
        public DateTime FechaRegistro { get; set; }
        public int MortalidadHembras { get; set; }
        public int MortalidadMachos { get; set; }
        public int SelH { get; set; }
        public int SelM { get; set; }
        public int ErrorSexajeHembras { get; set; }
        public int ErrorSexajeMachos { get; set; }
        // Traslados de aves del día. Entran en el saldo con el mismo signo que en
        // fn_reporte_semanal_levante_extras (salida resta, ingreso suma); antes se ignoraban.
        public int TrasladoSalidaHembras { get; set; }
        public int TrasladoSalidaMachos { get; set; }
        public int TrasladoIngresoHembras { get; set; }
        public int TrasladoIngresoMachos { get; set; }
        // Venta de aves del día. Sale del lote igual que un traslado de salida, sólo que no llega a
        // ningún otro lote. Se usan los splits por sexo (no `VentaAvesCantidad`, que es el total sin
        // distinguir) porque este reporte lleva el saldo por sexo — mismo criterio que
        // fn_reporte_semanal_levante_extras y fn_indicadores_levante_postura.
        public int VentaAvesHembras { get; set; }
        public int VentaAvesMachos { get; set; }
        public double ConsumoKgHembras { get; set; }
        public double? ConsumoKgMachos { get; set; }
        public double? PesoPromH { get; set; }
        public double? PesoPromM { get; set; }
        public double? UniformidadH { get; set; }
        public double? UniformidadM { get; set; }
        public double? CvH { get; set; }
        public double? CvM { get; set; }
        public double? KcalAlH { get; set; }
        public double? ProtAlH { get; set; }
        public double? KcalAveH { get; set; }
        public double? ProtAveH { get; set; }
        public string? Observaciones { get; set; }
    }

    private const string TipoLevante = "levante";

    /// <summary>Obtiene todos los seguimientos de levante del lote desde la tabla unificada seguimiento_diario (fase levante), por lote_id (legacy).</summary>
    private async Task<List<SegLevanteParaReporte>> ObtenerSeguimientosLevanteUnificadoAsync(int loteId, CancellationToken ct)
    {
        var loteIdStr = loteId.ToString();
        var list = await _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == TipoLevante && s.LoteId == loteIdStr)
            .OrderBy(s => s.Fecha)
            .Select(s => new SegLevanteParaReporte
            {
                Id = (int)s.Id,
                LoteId = loteId,
                FechaRegistro = s.Fecha,
                MortalidadHembras = s.MortalidadHembras ?? 0,
                MortalidadMachos = s.MortalidadMachos ?? 0,
                SelH = s.SelH ?? 0,
                SelM = s.SelM ?? 0,
                ErrorSexajeHembras = s.ErrorSexajeHembras ?? 0,
                ErrorSexajeMachos = s.ErrorSexajeMachos ?? 0,
                TrasladoSalidaHembras = s.TrasladoSalidaHembras,
                TrasladoSalidaMachos = s.TrasladoSalidaMachos,
                TrasladoIngresoHembras = s.TrasladoIngresoHembras,
                TrasladoIngresoMachos = s.TrasladoIngresoMachos,
                VentaAvesHembras = s.VentaAvesHembras,
                VentaAvesMachos = s.VentaAvesMachos,
                ConsumoKgHembras = (double)(s.ConsumoKgHembras ?? 0),
                ConsumoKgMachos = s.ConsumoKgMachos.HasValue ? (double)s.ConsumoKgMachos.Value : null,
                PesoPromH = s.PesoPromHembras,
                PesoPromM = s.PesoPromMachos,
                UniformidadH = s.UniformidadHembras,
                UniformidadM = s.UniformidadMachos,
                CvH = s.CvHembras,
                CvM = s.CvMachos,
                KcalAlH = s.KcalAlH,
                ProtAlH = s.ProtAlH,
                KcalAveH = s.KcalAveH,
                ProtAveH = s.ProtAveH,
                Observaciones = s.Observaciones
            })
            .ToListAsync(ct);
        return list;
    }

    /// <summary>Obtiene seguimientos de levante por lote_postura_levante_id (seguimiento_diario.lote_postura_levante_id).</summary>
    private async Task<List<SegLevanteParaReporte>> ObtenerSeguimientosLevantePorLPLAsync(int lotePosturaLevanteId, CancellationToken ct)
    {
        var list = await _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == TipoLevante && s.LotePosturaLevanteId == lotePosturaLevanteId)
            .OrderBy(s => s.Fecha)
            .Select(s => new SegLevanteParaReporte
            {
                Id = (int)s.Id,
                LoteId = lotePosturaLevanteId,
                FechaRegistro = s.Fecha,
                MortalidadHembras = s.MortalidadHembras ?? 0,
                MortalidadMachos = s.MortalidadMachos ?? 0,
                SelH = s.SelH ?? 0,
                SelM = s.SelM ?? 0,
                ErrorSexajeHembras = s.ErrorSexajeHembras ?? 0,
                ErrorSexajeMachos = s.ErrorSexajeMachos ?? 0,
                TrasladoSalidaHembras = s.TrasladoSalidaHembras,
                TrasladoSalidaMachos = s.TrasladoSalidaMachos,
                TrasladoIngresoHembras = s.TrasladoIngresoHembras,
                TrasladoIngresoMachos = s.TrasladoIngresoMachos,
                VentaAvesHembras = s.VentaAvesHembras,
                VentaAvesMachos = s.VentaAvesMachos,
                ConsumoKgHembras = (double)(s.ConsumoKgHembras ?? 0),
                ConsumoKgMachos = s.ConsumoKgMachos.HasValue ? (double)s.ConsumoKgMachos.Value : null,
                PesoPromH = s.PesoPromHembras,
                PesoPromM = s.PesoPromMachos,
                UniformidadH = s.UniformidadHembras,
                UniformidadM = s.UniformidadMachos,
                CvH = s.CvHembras,
                CvM = s.CvMachos,
                KcalAlH = s.KcalAlH,
                ProtAlH = s.ProtAlH,
                KcalAveH = s.KcalAveH,
                ProtAveH = s.ProtAveH,
                Observaciones = s.Observaciones
            })
            .ToListAsync(ct);
        return list;
    }

    private async Task<bool> EsLoteEnLevanteAsync(int loteId, CancellationToken ct)
    {
        // Obtener información del lote para calcular la edad
        var lote = await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteId, ct);

        if (lote == null || !lote.FechaEncaset.HasValue)
        {
            // Si no hay fecha de encaset, verificar por registros en tabla unificada (fase levante)
            var tieneRegistros = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .AnyAsync(s => s.TipoSeguimiento == TipoLevante && s.LoteId == loteId.ToString(), ct);
            return tieneRegistros;
        }

        // Calcular edad en días
        var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, DateTime.Now);
        
        // Levante es hasta 25 semanas (175 días)
        // Producción es desde la semana 26 (176 días en adelante)
        if (edadDias < 175)
        {
            // Está en levante por edad
            return true;
        }
        
        // Está en producción por edad, pero verificar si tiene registros en producción
        var tieneProduccion = await _ctx.SeguimientoProduccion
            .AsNoTracking()
            .AnyAsync(s => s.LoteId == loteId, ct);
        
        // Si tiene registros en producción, definitivamente está en producción
        if (tieneProduccion)
            return false;
        
        // Si tiene más de 175 días, está en producción aunque tenga registros históricos en levante
        return false; // Está en producción por edad
    }
}
