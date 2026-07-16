// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionCronogramaItemRow.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Proyección 1:1 de fn_vacunacion_cronograma_lote(...). Mapeada por SqlQueryRaw
/// (snake_case → PascalCase). El registro viene aplanado (registro_id NULL = sin registro);
/// <c>VacunacionCronogramaMapper.ToDto</c> arma el DTO anidado.</summary>
public class VacunacionCronogramaItemRow
{
    public int Id { get; set; }
    public string LineaProductiva { get; set; } = "";
    public int LoteId { get; set; }
    public string? LoteNombre { get; set; }
    public int GranjaId { get; set; }
    public string? GranjaNombre { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int ItemInventarioId { get; set; }
    public string? ItemInventarioNombre { get; set; }
    public string UnidadObjetivo { get; set; } = "";
    public int? ValorObjetivo { get; set; }
    public DateTime? FechaObjetivo { get; set; }
    public int RangoDiasAntes { get; set; }
    public int RangoDiasDespues { get; set; }
    public DateTime? FechaInicioFranja { get; set; }
    public DateTime? FechaFinFranja { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
    public string? Notas { get; set; }
    public int? RegistroId { get; set; }
    public string? RegistroEstado { get; set; }
    public DateTime? RegistroFechaAplicacion { get; set; }
    public int? RegistroDiasDesviacion { get; set; }
    public bool? RegistroIncumplido { get; set; }
    public string? RegistroMotivo { get; set; }
    public int? UsuarioRegistraId { get; set; }
    public string? UsuarioRegistraNombre { get; set; }
    public int? AplicadoPorUserId { get; set; }
    public string? AplicadoPorUserNombre { get; set; }
    public string? AplicadoPorNombreLibre { get; set; }
}
