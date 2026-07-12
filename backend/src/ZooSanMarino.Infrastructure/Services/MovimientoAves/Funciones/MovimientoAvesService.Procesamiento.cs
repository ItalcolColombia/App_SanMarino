// MovimientoAves/Funciones/MovimientoAvesService.Procesamiento.cs
// Procesar y cancelar un movimiento: aplica/revierte inventario, seguimiento diario y tablas postura.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    public async Task<ResultadoMovimientoDto> ProcesarMovimientoAsync(ProcesarMovimientoDto dto)
    {
        var movimiento = await _context.MovimientoAves
            .Where(m => m.Id == dto.MovimientoId && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (movimiento == null)
            return new ResultadoMovimientoDto(false, "Movimiento no encontrado", null, null, new List<string> { "Movimiento no encontrado" }, null);

        if (movimiento.Estado != "Pendiente")
            return new ResultadoMovimientoDto(false, "El movimiento ya fue procesado o cancelado", null, null, new List<string> { "Estado inválido" }, null);

        try
        {
            // Procesar el movimiento (implementación básica)
            movimiento.Procesar();
            if (!string.IsNullOrEmpty(dto.ObservacionesProcesamiento))
                movimiento.Observaciones = $"{movimiento.Observaciones} | {dto.ObservacionesProcesamiento}";

            await _context.SaveChangesAsync();

            // Actualizar inventario del lote al procesar el movimiento
            await ActualizarInventarioPorMovimientoAsync(movimiento, dto.AutoCrearInventarioDestino);

            // Si es un traslado de aves, aplicar descuento según la etapa del lote (levante o producción)
            if (movimiento.LoteOrigenId.HasValue &&
                (movimiento.TipoMovimiento == "Traslado" || movimiento.TipoMovimiento == "Venta"))
            {
                // Aplicar descuento en producción (semana 26+) - RESTAR del origen
                await AplicarDescuentoEnProduccionDiariaAvesAsync(movimiento);

                // Aplicar descuento en levante (semana < 26) - RESTAR del origen
                await AplicarDescuentoEnLevanteDiariaAvesAsync(movimiento);
            }

            // Si es un traslado entre lotes, crear registro de entrada en el lote destino
            if (movimiento.LoteDestinoId.HasValue && movimiento.TipoMovimiento == "Traslado")
            {
                // Crear registro de entrada en seguimiento diario del lote destino
                await CrearRegistroEntradaEnLoteDestinoAsync(movimiento);
            }

            // Actualizar AvesHActual/AvesMActual directamente en las tablas postura (fuente primaria de inventario).
            // Resta del lote origen (venta o traslado) y suma al lote destino (traslado).
            // La fase se determina con tres señales: Lote.Fase, EstadoCierre=="Cerrado", semana>=26.
            await ActualizarAvesActualesEnPosturaAsync(movimiento);

            var movimientoDto = await GetByIdAsync(movimiento.Id);
            return new ResultadoMovimientoDto(true, "Movimiento procesado exitosamente", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimientoDto);
        }
        catch (Exception ex)
        {
            return new ResultadoMovimientoDto(false, "Error al procesar movimiento", movimiento.Id, movimiento.NumeroMovimiento, new List<string> { ex.Message }, null);
        }
    }

    public async Task<ResultadoMovimientoDto> CancelarMovimientoAsync(CancelarMovimientoDto dto)
    {
        var movimiento = await _context.MovimientoAves
            .Where(m => m.Id == dto.MovimientoId && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (movimiento == null)
            return new ResultadoMovimientoDto(false, "Movimiento no encontrado", null, null, new List<string> { "Movimiento no encontrado" }, null);

        // Si ya está cancelado, no hacer nada
        if (movimiento.Estado == "Cancelado")
        {
            var movimientoDto = await GetByIdAsync(movimiento.Id);
            return new ResultadoMovimientoDto(true, "Movimiento ya estaba cancelado", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimientoDto);
        }

        try
        {
            // Si el movimiento está en estado "Completado", significa que ya se aplicó el descuento
            // Necesitamos devolver las aves al inventario antes de cancelar
            if (movimiento.Estado == "Completado")
            {
                // Devolver las aves al inventario antes de cancelar
                await DevolverAvesAlInventarioAsync(movimiento);
            }
            // Si está en "Pendiente", también puede haber descuento aplicado (si se procesó automáticamente)
            else if (movimiento.Estado == "Pendiente")
            {
                // Verificar si hay descuento aplicado y devolver las aves
                await DevolverAvesAlInventarioAsync(movimiento);
            }

            // Cancelar el movimiento
            if (movimiento.Estado == "Completado")
            {
                // Permitir cancelar movimientos completados para devolver aves
                movimiento.Estado = "Cancelado";
                movimiento.FechaCancelacion = DateTime.UtcNow;
                var motivoCompleto = $"Cancelado (movimiento completado): {dto.MotivoCancelacion}";
                movimiento.Observaciones = string.IsNullOrEmpty(movimiento.Observaciones)
                    ? motivoCompleto
                    : $"{movimiento.Observaciones} | {motivoCompleto}";
            }
            else
            {
                // Usar el método de dominio para cancelar movimientos pendientes
                movimiento.Cancelar(dto.MotivoCancelacion);
            }

            await _context.SaveChangesAsync();

            var movimientoDto = await GetByIdAsync(movimiento.Id);
            return new ResultadoMovimientoDto(true, "Movimiento cancelado exitosamente", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimientoDto);
        }
        catch (Exception ex)
        {
            return new ResultadoMovimientoDto(false, "Error al cancelar movimiento", movimiento.Id, movimiento.NumeroMovimiento, new List<string> { ex.Message }, null);
        }
    }
}
