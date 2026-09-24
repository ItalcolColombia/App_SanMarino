// Novedades persistentes del Home (plan §6.8, §10.4, casos 14-15, 36-39).
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.FlujosValidacion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class FlujoValidacionService
{
    public async Task<IReadOnlyList<NovedadValidacionDto>> ObtenerMisNovedadesAsync(CancellationToken ct = default)
    {
        if (!_current.UserGuid.HasValue) return Array.Empty<NovedadValidacionDto>();

        var novedades = await _ctx.ValidacionNovedadesUsuario.AsNoTracking()
            .Include(n => n.Instancia).ThenInclude(i => i.Proceso)
            .Include(n => n.Instancia).ThenInclude(i => i.Company)
            .Include(n => n.AccionDevolucion)
            .Where(n => n.CompanyId == CompanyIdActiva
                     && n.DestinatarioUserId == _current.UserGuid.Value
                     && n.Estado == EstadoNovedadValidacion.Activa)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        var generadoresPorId = await _ctx.Users.AsNoTracking()
            .Where(u => novedades.Select(n => n.GeneradaPorUserId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.firstName} {u.surName}".Trim(), ct);

        var pasosNombrePorId = await _ctx.ValidacionInstanciaPasos.AsNoTracking()
            .Where(p => novedades.Select(n => n.AccionDevolucion.InstanciaPasoId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var resultado = new List<NovedadValidacionDto>();
        foreach (var n in novedades)
        {
            var pasoQueDevolvio = pasosNombrePorId.GetValueOrDefault(n.AccionDevolucion.InstanciaPasoId);
            var ctx = JsonSerializer.Deserialize<JsonElement>(n.ContextoResumen);
            string? Leer(string prop) => ctx.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            resultado.Add(new NovedadValidacionDto(
                n.Id, n.InstanciaId, n.Instancia.Proceso.Key, n.Instancia.Company.Name,
                Leer("farm") ?? "", Leer("nucleo"), Leer("galpon"), Leer("lote"),
                ctx.TryGetProperty("fecha", out var f) && f.ValueKind == JsonValueKind.String && DateOnly.TryParse(f.GetString(), out var fecha)
                    ? fecha : default,
                pasoQueDevolvio?.Orden ?? 0, pasoQueDevolvio?.Nombre ?? "",
                generadoresPorId.GetValueOrDefault(n.GeneradaPorUserId, "(usuario)"),
                n.Motivo, n.CreatedAt, n.ReadAt));
        }
        return resultado;
    }

    /// <summary>
    /// Marca la novedad como leída. NO la resuelve ni la oculta (regla §4.15 del plan): sigue
    /// ACTIVA hasta que se corrija/reenvíe, se devuelva otra etapa o se elimine el registro.
    /// </summary>
    public async Task MarcarNovedadLeidaAsync(long novedadId, CancellationToken ct = default)
    {
        if (!_current.UserGuid.HasValue) return;

        await _ctx.ValidacionNovedadesUsuario
            .Where(n => n.Id == novedadId && n.DestinatarioUserId == _current.UserGuid.Value && n.ReadAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
    }
}
