// MovimientoAves/Funciones/MovimientoAvesService.Crud.cs
// Alta, edición y eliminación lógica del movimiento (con validación, numeración y reversión de
// efectos en inventario/postura cuando el movimiento eliminado estaba completado).
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    public async Task<MovimientoAvesDto> CreateAsync(CreateMovimientoAvesDto dto)
    {
        // Validar movimiento
        var esValido = await ValidarMovimientoAsync(dto);
        if (!esValido)
            throw new InvalidOperationException("El movimiento no es válido");

        var movimiento = new MovimientoAves
        {
            FechaMovimiento = dto.FechaMovimiento,
            TipoMovimiento = dto.TipoMovimiento,
            InventarioOrigenId = dto.InventarioOrigenId,
            LoteOrigenId = dto.LoteOrigenId,
            GranjaOrigenId = dto.GranjaOrigenId,
            NucleoOrigenId = dto.NucleoOrigenId,
            GalponOrigenId = dto.GalponOrigenId,
            InventarioDestinoId = dto.InventarioDestinoId,
            LoteDestinoId = dto.LoteDestinoId,
            GranjaDestinoId = dto.GranjaDestinoId,
            NucleoDestinoId = dto.NucleoDestinoId,
            GalponDestinoId = dto.GalponDestinoId,
            PlantaDestino = dto.PlantaDestino,
            CantidadHembras = dto.CantidadHembras,
            CantidadMachos = dto.CantidadMachos,
            CantidadMixtas = dto.CantidadMixtas,
            MotivoMovimiento = dto.MotivoMovimiento,
            Descripcion = dto.Descripcion,
            Observaciones = dto.Observaciones,
            // Campos específicos para despacho (Ecuador)
            EdadAves = dto.EdadAves,
            Raza = dto.Raza,
            Placa = dto.Placa,
            HoraSalida = dto.HoraSalida,
            GuiaAgrocalidad = dto.GuiaAgrocalidad,
            Sellos = dto.Sellos,
            Ayuno = dto.Ayuno,
            Conductor = dto.Conductor,
            TotalPollosGalpon = dto.TotalPollosGalpon,
            PesoBruto = dto.PesoBruto,
            PesoTara = dto.PesoTara,
            Estado = "Pendiente",
            UsuarioMovimientoId = dto.UsuarioMovimientoId > 0 ? dto.UsuarioMovimientoId : _currentUser.UserId,
            CompanyId = _currentUser.CompanyId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };


        // Agregar al contexto y guardar para obtener el ID
        _context.MovimientoAves.Add(movimiento);
        await _context.SaveChangesAsync();

        // Generar número de movimiento con el ID obtenido
        movimiento.NumeroMovimiento = $"MOV-{DateTime.UtcNow:yyyyMMdd}-{movimiento.Id:D6}";
        await _context.SaveChangesAsync();

        // Procesar automáticamente el movimiento (aplicar cambios en inventario y seguimiento diario)
        try
        {
            await ProcesarMovimientoAsync(new ProcesarMovimientoDto
            {
                MovimientoId = movimiento.Id,
                AutoCrearInventarioDestino = true
            });
        }
        catch (Exception ex)
        {
            // Si falla el procesamiento, marcar el movimiento como pendiente para que se pueda procesar manualmente
            _logger.LogError(ex, "Error al procesar automáticamente el movimiento {MovimientoId}", movimiento.Id);
            // No lanzar excepción, dejar el movimiento en estado Pendiente
        }

        return await GetByIdAsync(movimiento.Id) ?? throw new InvalidOperationException("Error al crear movimiento");
    }

    public async Task<ResultadoMovimientoDto> EliminarMovimientoAsync(int id)
    {
        var movimiento = await _context.MovimientoAves
            .Where(m => m.Id == id && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (movimiento == null)
            return new ResultadoMovimientoDto(false, "Movimiento no encontrado", null, null, new List<string> { "Movimiento no encontrado" }, null);

        try
        {
            // Si estaba Completado, revertir su efecto en las tablas postura y en InventarioAves
            if (movimiento.Estado == "Completado")
            {
                await RevertirAvesActualesEnPosturaAsync(movimiento);

                if (movimiento.LoteOrigenId.HasValue && movimiento.GranjaOrigenId.HasValue)
                {
                    var inventarioOrigen = await ObtenerOCrearInventarioAsync(
                        movimiento.LoteOrigenId.Value, movimiento.GranjaOrigenId.Value,
                        movimiento.NucleoOrigenId, movimiento.GalponOrigenId, crearSiNoExiste: false);
                    if (inventarioOrigen != null)
                    {
                        inventarioOrigen.AplicarMovimientoEntrada(movimiento.CantidadHembras, movimiento.CantidadMachos, movimiento.CantidadMixtas);
                        inventarioOrigen.UpdatedAt = DateTime.UtcNow;
                    }
                }

                if (movimiento.LoteDestinoId.HasValue && movimiento.GranjaDestinoId.HasValue && movimiento.TipoMovimiento == "Traslado")
                {
                    var inventarioDestino = await ObtenerOCrearInventarioAsync(
                        movimiento.LoteDestinoId.Value, movimiento.GranjaDestinoId.Value,
                        movimiento.NucleoDestinoId, movimiento.GalponDestinoId, crearSiNoExiste: false);
                    if (inventarioDestino != null)
                    {
                        inventarioDestino.AplicarMovimientoSalida(movimiento.CantidadHembras, movimiento.CantidadMachos, movimiento.CantidadMixtas);
                        inventarioDestino.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            // Eliminación lógica
            movimiento.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new ResultadoMovimientoDto(true, "Movimiento eliminado exitosamente", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), null);
        }
        catch (Exception ex)
        {
            return new ResultadoMovimientoDto(false, "Error al eliminar movimiento", movimiento.Id, movimiento.NumeroMovimiento, new List<string> { ex.Message }, null);
        }
    }

    /// <summary>
    /// Actualiza un movimiento de aves existente
    /// </summary>
    public async Task<MovimientoAvesDto> ActualizarMovimientoAvesAsync(int movimientoId, ActualizarMovimientoAvesDto dto, int usuarioId)
    {
        var movimiento = await _context.MovimientoAves
            .FirstOrDefaultAsync(m =>
                m.Id == movimientoId &&
                m.CompanyId == _currentUser.CompanyId &&
                m.DeletedAt == null);

        if (movimiento == null)
        {
            throw new InvalidOperationException($"Movimiento {movimientoId} no encontrado");
        }

        if (movimiento.Estado != "Pendiente")
        {
            throw new InvalidOperationException($"Solo se pueden actualizar movimientos en estado 'Pendiente'. El movimiento actual está en estado '{movimiento.Estado}'");
        }

        // Guardar las cantidades originales antes de la actualización
        var cantidadesOriginales = new Dictionary<string, int>
        {
            { "Hembras", movimiento.CantidadHembras },
            { "Machos", movimiento.CantidadMachos },
            { "Mixtas", movimiento.CantidadMixtas }
        };

        // Si se actualizan las cantidades, validar disponibilidad
        if (dto.CantidadHembras.HasValue || dto.CantidadMachos.HasValue || dto.CantidadMixtas.HasValue)
        {
            var nuevasCantidades = new Dictionary<string, int>
            {
                { "Hembras", dto.CantidadHembras ?? movimiento.CantidadHembras },
                { "Machos", dto.CantidadMachos ?? movimiento.CantidadMachos },
                { "Mixtas", dto.CantidadMixtas ?? movimiento.CantidadMixtas }
            };

            var cantidadesParaValidar = new Dictionary<string, int>();
            foreach (var tipo in nuevasCantidades.Keys)
            {
                var diferencia = nuevasCantidades[tipo] - cantidadesOriginales[tipo];
                if (diferencia > 0) // Solo validar si estamos pidiendo más aves
                {
                    cantidadesParaValidar[tipo] = diferencia;
                }
            }

            if (cantidadesParaValidar.Count > 0 && movimiento.LoteOrigenId.HasValue)
            {
                var inventarioOrigen = await ObtenerOCrearInventarioAsync(
                    movimiento.LoteOrigenId.Value,
                    movimiento.GranjaOrigenId ?? 0,
                    movimiento.NucleoOrigenId,
                    movimiento.GalponOrigenId,
                    crearSiNoExiste: false);

                if (inventarioOrigen != null)
                {
                    var hembrasDisponibles = inventarioOrigen.CantidadHembras + cantidadesOriginales["Hembras"];
                    var machosDisponibles = inventarioOrigen.CantidadMachos + cantidadesOriginales["Machos"];
                    var mixtasDisponibles = inventarioOrigen.CantidadMixtas + cantidadesOriginales["Mixtas"];

                    if (nuevasCantidades["Hembras"] > hembrasDisponibles ||
                        nuevasCantidades["Machos"] > machosDisponibles ||
                        nuevasCantidades["Mixtas"] > mixtasDisponibles)
                    {
                        throw new InvalidOperationException("No hay suficientes aves disponibles para esta actualización. Las nuevas cantidades exceden la disponibilidad actual.");
                    }
                }
            }
        }

        // Actualizar campos del movimiento
        movimiento.FechaMovimiento = dto.FechaMovimiento ?? movimiento.FechaMovimiento;
        movimiento.TipoMovimiento = dto.TipoMovimiento ?? movimiento.TipoMovimiento;
        movimiento.LoteOrigenId = dto.LoteOrigenId ?? movimiento.LoteOrigenId;
        movimiento.GranjaOrigenId = dto.GranjaOrigenId ?? movimiento.GranjaOrigenId;
        movimiento.NucleoOrigenId = dto.NucleoOrigenId ?? movimiento.NucleoOrigenId;
        movimiento.GalponOrigenId = dto.GalponOrigenId ?? movimiento.GalponOrigenId;
        movimiento.LoteDestinoId = dto.LoteDestinoId ?? movimiento.LoteDestinoId;
        movimiento.GranjaDestinoId = dto.GranjaDestinoId ?? movimiento.GranjaDestinoId;
        movimiento.NucleoDestinoId = dto.NucleoDestinoId ?? movimiento.NucleoDestinoId;
        movimiento.GalponDestinoId = dto.GalponDestinoId ?? movimiento.GalponDestinoId;
        movimiento.CantidadHembras = dto.CantidadHembras ?? movimiento.CantidadHembras;
        movimiento.CantidadMachos = dto.CantidadMachos ?? movimiento.CantidadMachos;
        movimiento.CantidadMixtas = dto.CantidadMixtas ?? movimiento.CantidadMixtas;
        movimiento.MotivoMovimiento = dto.MotivoMovimiento ?? movimiento.MotivoMovimiento;
        movimiento.PlantaDestino = dto.PlantaDestino ?? movimiento.PlantaDestino;
        movimiento.Descripcion = dto.Descripcion ?? movimiento.Descripcion;
        movimiento.Observaciones = dto.Observaciones ?? movimiento.Observaciones;

        // Campos específicos para despacho (Ecuador)
        if (dto.EdadAves.HasValue)
            movimiento.EdadAves = dto.EdadAves;
        if (dto.Raza != null)
            movimiento.Raza = dto.Raza;
        if (dto.Placa != null)
            movimiento.Placa = dto.Placa;
        if (dto.HoraSalida.HasValue)
            movimiento.HoraSalida = dto.HoraSalida;
        if (dto.GuiaAgrocalidad != null)
            movimiento.GuiaAgrocalidad = dto.GuiaAgrocalidad;
        if (dto.Sellos != null)
            movimiento.Sellos = dto.Sellos;
        if (dto.Ayuno != null)
            movimiento.Ayuno = dto.Ayuno;
        if (dto.Conductor != null)
            movimiento.Conductor = dto.Conductor;
        if (dto.TotalPollosGalpon.HasValue)
            movimiento.TotalPollosGalpon = dto.TotalPollosGalpon;
        if (dto.PesoBruto.HasValue)
            movimiento.PesoBruto = dto.PesoBruto;
        if (dto.PesoTara.HasValue)
            movimiento.PesoTara = dto.PesoTara;

        movimiento.UpdatedByUserId = usuarioId;
        movimiento.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Si se actualizaron las cantidades y el movimiento ya estaba procesado, ajustar seguimiento diario
        if (movimiento.Estado == "Completado" &&
            (dto.CantidadHembras.HasValue || dto.CantidadMachos.HasValue || dto.CantidadMixtas.HasValue))
        {
            // Ajustar los registros en seguimiento diario
            await AjustarSeguimientoDiarioPorEdicionAsync(movimiento, cantidadesOriginales);
        }

        return await GetByIdAsync(movimiento.Id) ?? throw new InvalidOperationException("Error al actualizar movimiento");
    }
}
