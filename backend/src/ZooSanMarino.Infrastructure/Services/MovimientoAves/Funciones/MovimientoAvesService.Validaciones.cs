// MovimientoAves/Funciones/MovimientoAvesService.Validaciones.cs
// Validaciones de un movimiento antes de crearlo/procesarlo: cantidades, origen/destino,
// disponibilidad de aves y existencia de la ubicación destino.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    public async Task<bool> ValidarMovimientoAsync(CreateMovimientoAvesDto dto)
    {
        // Cantidades > 0
        var total = dto.CantidadHembras + dto.CantidadMachos + dto.CantidadMixtas;
        if (total <= 0) return false;

        // Debe existir un origen (inventario o lote)
        var tieneOrigen = dto.InventarioOrigenId.HasValue || dto.LoteOrigenId.HasValue;
        if (!tieneOrigen) return false;

        // Para retiros y ventas, no se requiere destino; para otros tipos (traslados), sí
        var esRetiro = dto.TipoMovimiento?.Equals("Retiro", StringComparison.OrdinalIgnoreCase) == true;
        var esVenta = dto.TipoMovimiento?.Equals("Venta", StringComparison.OrdinalIgnoreCase) == true;
        var noRequiereDestino = esRetiro || esVenta;

        if (!noRequiereDestino)
        {
            var tieneDestino = dto.InventarioDestinoId.HasValue || dto.LoteDestinoId.HasValue || !string.IsNullOrWhiteSpace(dto.PlantaDestino);
            if (!tieneDestino) return false;

            // Origen y destino no pueden ser el mismo lote (excepto para retiros y ventas)
            if (dto.LoteOrigenId.HasValue && dto.LoteDestinoId.HasValue &&
                dto.LoteOrigenId.Value == dto.LoteDestinoId.Value)
                return false;
        }

        // No cantidades negativas
        if (dto.CantidadHembras < 0 || dto.CantidadMachos < 0 || dto.CantidadMixtas < 0)
            return false;

        // Normaliza tipo
        dto.TipoMovimiento = string.IsNullOrWhiteSpace(dto.TipoMovimiento)
            ? "Traslado"
            : char.ToUpper(dto.TipoMovimiento[0]) + dto.TipoMovimiento.Substring(1).ToLower();

        // Verifica existencia de lotes si llegan
        if (dto.LoteOrigenId.HasValue)
            if (!await _context.Lotes.AnyAsync(l => l.LoteId == dto.LoteOrigenId.Value && l.CompanyId == _currentUser.CompanyId))
                return false;

        if (dto.LoteDestinoId.HasValue)
            if (!await _context.Lotes.AnyAsync(l => l.LoteId == dto.LoteDestinoId.Value && l.CompanyId == _currentUser.CompanyId))
                return false;

        return true;
    }

    public async Task<List<string>> ValidarDisponibilidadAvesAsync(int inventarioOrigenId, int hembras, int machos, int mixtas)
    {
        var errores = new List<string>();

        var puedeRealizar = await _inventarioService.PuedeRealizarMovimientoAsync(inventarioOrigenId, hembras, machos, mixtas);
        if (!puedeRealizar)
            errores.Add("No hay suficientes aves disponibles para el movimiento");

        return errores;
    }

    public async Task<bool> ValidarUbicacionDestinoAsync(int granjaId, string? nucleoId, string? galponId)
    {
        // Validación básica - verificar que la granja existe
        return await _context.Farms
            .Where(f => f.Id == granjaId && f.CompanyId == _currentUser.CompanyId)
            .AnyAsync();
    }
}
