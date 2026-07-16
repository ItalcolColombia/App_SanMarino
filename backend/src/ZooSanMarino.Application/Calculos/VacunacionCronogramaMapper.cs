// src/ZooSanMarino.Application/Calculos/VacunacionCronogramaMapper.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Mapeo PURO fila de fn_vacunacion_cronograma_lote → <see cref="VacunacionCronogramaItemDto"/>.
/// La franja viene calculada en SQL con la misma fórmula de <see cref="VacunacionCalculos.CalcularFranja"/>;
/// si llega NULL (lote sin fecha de encaset con unidad Semana/Dia) se lanza la misma excepción que
/// el cálculo puro, para conservar el contrato del endpoint (400 con mensaje claro).
/// </summary>
public static class VacunacionCronogramaMapper
{
    public static VacunacionCronogramaItemDto ToDto(VacunacionCronogramaItemRow r)
    {
        if (!r.FechaInicioFranja.HasValue || !r.FechaFinFranja.HasValue)
            throw new InvalidOperationException(
                $"No se puede calcular la franja: unidadObjetivo='{r.UnidadObjetivo}' requiere fechaEncaset+valorObjetivo (Semana/Dia) o fechaObjetivo (Fecha).");

        VacunacionRegistroAplicacionDto? registro = null;
        if (r.RegistroId.HasValue)
        {
            registro = new VacunacionRegistroAplicacionDto(
                r.RegistroId.Value,
                r.RegistroEstado ?? "Pendiente",
                r.RegistroFechaAplicacion,
                r.RegistroDiasDesviacion,
                r.RegistroIncumplido ?? false,
                r.RegistroMotivo,
                r.UsuarioRegistraId ?? 0,
                r.UsuarioRegistraNombre,
                r.AplicadoPorUserId,
                r.AplicadoPorUserNombre,
                r.AplicadoPorNombreLibre);
        }

        return new VacunacionCronogramaItemDto(
            r.Id, r.LineaProductiva, r.LoteId, r.LoteNombre ?? "",
            r.GranjaId, r.GranjaNombre, r.NucleoId, r.GalponId,
            r.ItemInventarioId, r.ItemInventarioNombre ?? "",
            r.UnidadObjetivo, r.ValorObjetivo, r.FechaObjetivo,
            r.RangoDiasAntes, r.RangoDiasDespues,
            r.FechaInicioFranja.Value, r.FechaFinFranja.Value,
            r.Orden, r.Activo, r.Notas, registro);
    }
}
