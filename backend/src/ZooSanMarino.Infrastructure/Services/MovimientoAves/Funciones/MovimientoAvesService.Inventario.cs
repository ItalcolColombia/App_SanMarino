// MovimientoAves/Funciones/MovimientoAvesService.Inventario.cs
// Efecto del movimiento sobre InventarioAves: aplicar salida/entrada al procesar, obtener o crear
// el inventario de un lote en su ubicación, y devolver las aves al cancelar.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    /// <summary>
    /// Actualiza el inventario del lote al procesar un movimiento (resta del origen, suma al destino)
    /// </summary>
    private async Task ActualizarInventarioPorMovimientoAsync(MovimientoAves movimiento, bool autoCrearInventarioDestino = true)
    {
        // Si es salida (Venta o Traslado desde origen)
        if (movimiento.LoteOrigenId.HasValue &&
            (movimiento.TipoMovimiento == "Traslado" || movimiento.TipoMovimiento == "Venta"))
        {
            if (!movimiento.GranjaOrigenId.HasValue)
            {
                throw new InvalidOperationException("El movimiento debe tener una granja de origen especificada.");
            }

            var inventarioOrigen = await ObtenerOCrearInventarioAsync(
                movimiento.LoteOrigenId.Value,
                movimiento.GranjaOrigenId.Value,
                movimiento.NucleoOrigenId,
                movimiento.GalponOrigenId);

            if (inventarioOrigen != null)
            {
                // Validar que hay suficientes aves disponibles
                if (!inventarioOrigen.PuedeRealizarMovimiento(
                    movimiento.CantidadHembras,
                    movimiento.CantidadMachos,
                    movimiento.CantidadMixtas))
                {
                    throw new InvalidOperationException(
                        $"No hay suficientes aves en el inventario del lote {movimiento.LoteOrigenId}. " +
                        $"Disponibles: H={inventarioOrigen.CantidadHembras}, M={inventarioOrigen.CantidadMachos}, Mixtas={inventarioOrigen.CantidadMixtas}. " +
                        $"Solicitadas: H={movimiento.CantidadHembras}, M={movimiento.CantidadMachos}, Mixtas={movimiento.CantidadMixtas}");
                }

                inventarioOrigen.AplicarMovimientoSalida(
                    movimiento.CantidadHembras,
                    movimiento.CantidadMachos,
                    movimiento.CantidadMixtas);

                inventarioOrigen.UpdatedByUserId = _currentUser.UserId;
                inventarioOrigen.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Si es entrada (Traslado a destino)
        if (movimiento.LoteDestinoId.HasValue && movimiento.TipoMovimiento == "Traslado")
        {
            var granjaDestinoId = movimiento.GranjaDestinoId ?? movimiento.GranjaOrigenId;
            if (!granjaDestinoId.HasValue)
            {
                throw new InvalidOperationException("El movimiento debe tener una granja de destino o origen especificada.");
            }

            var inventarioDestino = await ObtenerOCrearInventarioAsync(
                movimiento.LoteDestinoId.Value,
                granjaDestinoId.Value,
                movimiento.NucleoDestinoId,
                movimiento.GalponDestinoId,
                crearSiNoExiste: autoCrearInventarioDestino);

            if (inventarioDestino != null)
            {
                inventarioDestino.AplicarMovimientoEntrada(
                    movimiento.CantidadHembras,
                    movimiento.CantidadMachos,
                    movimiento.CantidadMixtas);

                inventarioDestino.UpdatedByUserId = _currentUser.UserId;
                inventarioDestino.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Obtiene o crea un inventario para un lote en una ubicación específica
    /// </summary>
    private async Task<InventarioAves?> ObtenerOCrearInventarioAsync(
        int loteId,
        int granjaId,
        string? nucleoId = null,
        string? galponId = null,
        bool crearSiNoExiste = true)
    {
        // Buscar inventario activo existente
        var inventario = await _context.InventarioAves
            .Where(i => i.LoteId == loteId &&
                       i.GranjaId == granjaId &&
                       (nucleoId == null || i.NucleoId == nucleoId) &&
                       (galponId == null || i.GalponId == galponId) &&
                       i.CompanyId == _currentUser.CompanyId &&
                       i.DeletedAt == null &&
                       i.Estado == "Activo")
            .OrderByDescending(i => i.FechaActualizacion)
            .FirstOrDefaultAsync();

        // Si no existe y se permite crear, crear uno nuevo
        if (inventario == null && crearSiNoExiste)
        {
            // Obtener información del lote para inicializar el inventario
            var lote = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.LoteId == loteId &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (lote == null)
                return null;

            // Crear inventario con cantidades iniciales del lote
            inventario = new InventarioAves
            {
                LoteId = loteId,
                GranjaId = granjaId,
                NucleoId = nucleoId ?? lote.NucleoId,
                GalponId = galponId ?? lote.GalponId,
                CantidadHembras = lote.HembrasL ?? 0,
                CantidadMachos = lote.MachosL ?? 0,
                CantidadMixtas = lote.Mixtas ?? 0,
                FechaActualizacion = DateTime.UtcNow,
                Estado = "Activo",
                CompanyId = _currentUser.CompanyId,
                CreatedByUserId = _currentUser.UserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.InventarioAves.Add(inventario);
            await _context.SaveChangesAsync();
        }

        return inventario;
    }

    /// <summary>
    /// Devuelve las aves al inventario cuando se cancela un movimiento
    /// </summary>
    private async Task DevolverAvesAlInventarioAsync(MovimientoAves movimiento)
    {
        // Si el movimiento ya fue procesado, necesitamos revertir los cambios en inventario y seguimiento diario
        if (movimiento.Estado == "Completado")
        {
            // Revertir cambios en inventario
            if (movimiento.LoteOrigenId.HasValue && movimiento.GranjaOrigenId.HasValue)
            {
                var inventarioOrigen = await ObtenerOCrearInventarioAsync(
                    movimiento.LoteOrigenId.Value,
                    movimiento.GranjaOrigenId.Value,
                    movimiento.NucleoOrigenId,
                    movimiento.GalponOrigenId,
                    crearSiNoExiste: false);

                if (inventarioOrigen != null)
                {
                    // Devolver las aves al inventario origen (sumar)
                    inventarioOrigen.AplicarMovimientoEntrada(
                        movimiento.CantidadHembras,
                        movimiento.CantidadMachos,
                        movimiento.CantidadMixtas);
                    inventarioOrigen.UpdatedByUserId = _currentUser.UserId;
                    inventarioOrigen.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Revertir cambios en inventario destino (restar)
            if (movimiento.LoteDestinoId.HasValue && movimiento.GranjaDestinoId.HasValue)
            {
                var inventarioDestino = await ObtenerOCrearInventarioAsync(
                    movimiento.LoteDestinoId.Value,
                    movimiento.GranjaDestinoId.Value,
                    movimiento.NucleoDestinoId,
                    movimiento.GalponDestinoId,
                    crearSiNoExiste: false);

                if (inventarioDestino != null)
                {
                    // Restar las aves del inventario destino
                    inventarioDestino.AplicarMovimientoSalida(
                        movimiento.CantidadHembras,
                        movimiento.CantidadMachos,
                        movimiento.CantidadMixtas);
                    inventarioDestino.UpdatedByUserId = _currentUser.UserId;
                    inventarioDestino.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            // Revertir cambios en seguimiento diario (Levante y Producción)
            if (movimiento.LoteOrigenId.HasValue)
            {
                await DevolverAvesEnSeguimientoDiarioAsync(movimiento);
            }

            // Revertir también en las tablas postura (fuente primaria de inventario)
            await RevertirAvesActualesEnPosturaAsync(movimiento);
        }
    }
}
