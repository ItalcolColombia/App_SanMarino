// Vacunacion/Funciones/VacunacionRegistroService.Registrar.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionRegistroService
{
    private async Task<VacunacionCronogramaItem> CargarItemAsync(int cronogramaItemId, CancellationToken ct)
    {
        var item = await _ctx.VacunacionCronogramaItem
            .Include(x => x.ItemInventario)
            .Include(x => x.RegistroAplicacion)
            .FirstOrDefaultAsync(x => x.Id == cronogramaItemId && x.CompanyId == _currentUser.CompanyId, ct)
            ?? throw new InvalidOperationException($"Ítem de cronograma {cronogramaItemId} no existe o no pertenece a la empresa activa.");

        if (item.RegistroAplicacion != null && item.RegistroAplicacion.Estado != VacunacionCalculos.EstadoPendiente)
            throw new InvalidOperationException("Este ítem del cronograma ya tiene una aplicación registrada.");

        return item;
    }

    private VacunacionRegistroAplicacion ObtenerOCrearRegistro(VacunacionCronogramaItem item, out bool esNuevo)
    {
        esNuevo = item.RegistroAplicacion is null;
        if (!esNuevo) return item.RegistroAplicacion!;

        var registro = new VacunacionRegistroAplicacion
        {
            VacunacionCronogramaItemId = item.Id,
            CompanyId = _currentUser.CompanyId,
            PaisId = _currentUser.PaisId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
        };
        item.RegistroAplicacion = registro;
        return registro;
    }

    /// <inheritdoc />
    public async Task<VacunacionCronogramaItemDto> RegistrarAplicadoAsync(
        int cronogramaItemId, VacunacionRegistrarAplicadoRequest req, CancellationToken ct = default)
    {
        var item = await CargarItemAsync(cronogramaItemId, ct);

        var tieneUsuario = req.AplicadoPorUserId.HasValue;
        var tieneLibre = !string.IsNullOrWhiteSpace(req.AplicadoPorNombreLibre);
        if (tieneUsuario == tieneLibre)
            throw new InvalidOperationException(
                "Debe indicar exactamente uno: el usuario del sistema que aplicó la vacuna, o el nombre libre del responsable.");

        var (fechaEncaset, loteNombre) = await ResolverLoteInfoAsync(item, ct);
        var franja = VacunacionCalculos.CalcularFranja(
            fechaEncaset, item.UnidadObjetivo, item.ValorObjetivo, item.FechaObjetivo,
            item.RangoDiasAntes, item.RangoDiasDespues);

        // Fecha de sistema al confirmar — NUNCA la que venga (o no venga) del request.
        var fechaAplicacion = DateTime.UtcNow.Date;
        var umbral = await GetUmbralIncumplidoAsync(ct);
        var resultado = VacunacionCalculos.CalcularEstadoAplicacion(franja, fechaAplicacion, umbral);

        if (resultado.RequiereMotivo && string.IsNullOrWhiteSpace(req.MotivoDescripcion))
            throw new InvalidOperationException(
                "La aplicación quedó fuera de la franja programada: debe indicar el motivo.");

        var registro = ObtenerOCrearRegistro(item, out var esNuevo);
        registro.Estado = resultado.Estado;
        registro.FechaAplicacion = fechaAplicacion;
        registro.DiasDesviacion = resultado.DiasDesviacion;
        registro.Incumplido = resultado.Incumplido;
        registro.MotivoDescripcion = string.IsNullOrWhiteSpace(req.MotivoDescripcion) ? null : req.MotivoDescripcion.Trim();
        registro.UsuarioRegistraId = _currentUser.UserId;
        registro.AplicadoPorUserId = req.AplicadoPorUserId;
        registro.AplicadoPorNombreLibre = tieneLibre ? req.AplicadoPorNombreLibre!.Trim() : null;
        if (!esNuevo)
        {
            registro.UpdatedByUserId = _currentUser.UserId;
            registro.UpdatedAt = DateTime.UtcNow;
        }

        if (esNuevo) _ctx.VacunacionRegistroAplicacion.Add(registro);
        await _ctx.SaveChangesAsync(ct);

        var granjaNombre = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == item.GranjaId).Select(f => f.Name).FirstOrDefaultAsync(ct);
        return VacunacionCronogramaService.MapItem(item, fechaEncaset, loteNombre, granjaNombre, item.ItemInventario?.Nombre ?? "");
    }

    /// <inheritdoc />
    public async Task<VacunacionCronogramaItemDto> RegistrarNoAplicadoAsync(
        int cronogramaItemId, VacunacionRegistrarNoAplicadoRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.MotivoDescripcion))
            throw new InvalidOperationException("El motivo es obligatorio para marcar 'no aplicado'.");

        var item = await CargarItemAsync(cronogramaItemId, ct);
        var (fechaEncaset, loteNombre) = await ResolverLoteInfoAsync(item, ct);
        var resultado = VacunacionCalculos.CalcularEstadoNoAplicado();

        var registro = ObtenerOCrearRegistro(item, out var esNuevo);
        registro.Estado = resultado.Estado;
        registro.FechaAplicacion = null;
        registro.DiasDesviacion = null;
        registro.Incumplido = false;
        registro.MotivoDescripcion = req.MotivoDescripcion.Trim();
        registro.UsuarioRegistraId = _currentUser.UserId;
        registro.AplicadoPorUserId = null;
        registro.AplicadoPorNombreLibre = null;
        if (!esNuevo)
        {
            registro.UpdatedByUserId = _currentUser.UserId;
            registro.UpdatedAt = DateTime.UtcNow;
        }

        if (esNuevo) _ctx.VacunacionRegistroAplicacion.Add(registro);
        await _ctx.SaveChangesAsync(ct);

        var granjaNombre = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == item.GranjaId).Select(f => f.Name).FirstOrDefaultAsync(ct);
        return VacunacionCronogramaService.MapItem(item, fechaEncaset, loteNombre, granjaNombre, item.ItemInventario?.Nombre ?? "");
    }
}
