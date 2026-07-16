// Modelos del sistema ORIGEN (ZooPanamaPollo) tal como los devuelve su API REST.
// Solo se LEE de ese sistema (GET); estos DTOs mapean el campo "result" de su envoltura
// ObjectGenericResult. Los nombres se enlazan por JSON case-insensitive (ver PuentePanamaApiClient),
// por eso no llevan atributos: coinciden con las claves camelCase del origen.
namespace ZooSanMarino.Application.DTOs.PuentePanama;

/// <summary>Envoltura genérica del API origen: { error, codError, message, result }.</summary>
public sealed class PanamaEnvelope<T>
{
    public bool Error { get; set; }
    public int CodError { get; set; }
    public string? Message { get; set; }
    public T? Result { get; set; }
}

/// <summary>Resultado del login: token JWT + expiración.</summary>
public sealed class PanamaLoginResult
{
    public string? Token { get; set; }
    public DateTime? Expiration { get; set; }
}

public sealed class PanamaCliente
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    /// <summary>Texto del departamento del cliente (ej. "PANAMA"). Se usa solo para resolver la geografía de sus granjas; el cliente NO se migra.</summary>
    public string? DepartamentoText { get; set; }
    /// <summary>Texto del municipio del cliente (ej. "Capira").</summary>
    public string? MunicipioText { get; set; }
}

/// <summary>Ítem del catálogo de Listas del origen (GetListtoOffLine). Tipo 1 = líneas genéticas (ROSS 308 AP, COBB 500…).</summary>
public sealed class PanamaListaItem
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public int IdTipoLista { get; set; }
    public bool Activo { get; set; }
}

public sealed class PanamaGranja
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public bool CertificadoGab { get; set; }
    public int IdCliente { get; set; }
}

public sealed class PanamaGalpon
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public double? Largo { get; set; }
    public double? Ancho { get; set; }
    public string? Tipogalpon { get; set; }
    public int IdGranja { get; set; }
}

public sealed class PanamaLote
{
    public int Id { get; set; }
    public string? CodLote { get; set; }
    public string? Nombre { get; set; }
    public string? LoteReproductora { get; set; }
    /// <summary>Línea genética del lote (referencia a lista). 0/null = el lote no tiene guía → pendiente por cargar.</summary>
    public int? IdLineaGeneticaLista { get; set; }
    public DateTime? FechaRegistroInicio { get; set; }
    public int? NumAvesEncasetadas { get; set; }
    public int? AvesHembra { get; set; }
    public int? AvesMacho { get; set; }
    public int? AvesMixta { get; set; }
    public double? PesoPromLlegHembra { get; set; }
    public double? PesoPromLlegMacho { get; set; }
    public double? PesoPromLlegMixt { get; set; }
    public int IdGalpon { get; set; }
}

/// <summary>InfoProductiva = seguimiento diario de engorde de un lote.</summary>
public sealed class PanamaInfoProductiva
{
    public int Id { get; set; }
    public DateTime? FechaRegistro { get; set; }
    public int? EdadDias { get; set; }
    public double? MortalidadHembra { get; set; }
    public double? MortalidadMacho { get; set; }
    public double? MortalidadMixta { get; set; }
    public double? SeleccionHembra { get; set; }
    public double? SeleccionMacho { get; set; }
    public double? SeleccionMixta { get; set; }
    public double? PesoHembra { get; set; }
    public double? PesoMacho { get; set; }
    public double? PesoMixta { get; set; }
    public double? Qqhembra { get; set; }
    public double? Qqmacho { get; set; }
    public double? Qqmixta { get; set; }
    public string? MarcaAlimento { get; set; }
    public string? FaseAlimentacion { get; set; }
    public string? Observacion { get; set; }
}

public sealed class PanamaLoteReproductora
{
    public int Id { get; set; }
    public string? Incubadora { get; set; }
    public string? NombreReproductora { get; set; }
    public int? AvesHembra { get; set; }
    public int? AvesMacho { get; set; }
    public int? AvesMixta { get; set; }
    public double? PesoLlegadaHembra { get; set; }
    public double? PesoLlegadaMacho { get; set; }
    public double? PesoLlegadaMixto { get; set; }
    public int IdLote { get; set; }
}

/// <summary>Guía genética del origen: estándar de consumo (gramoDiaQq = gramos de alimento por ave/día) por edad. No trae raza/año ni sexo.</summary>
public sealed class PanamaGuiaGenetica
{
    public int Id { get; set; }
    public int Edad { get; set; }
    public double GramoDiaQq { get; set; }
}

/// <summary>
/// Lesión registrada sobre un lote reproductora en el origen (GET /api/Lesion/GetLesionByIdLoteReproductora/{id}).
/// El origen devuelve además del id de lista (tipoLesionLista, Listas tipo 13: SL, ONF, CSV, PC…) el texto
/// resuelto en <see cref="LesionTipoText"/> (clave JSON "lesiontipotext").
/// </summary>
public sealed class PanamaLesion
{
    public int Id { get; set; }
    public DateTime? FechaRegistro { get; set; }
    public int? EdadDia { get; set; }
    public string? Observacion { get; set; }
    public int IdLoteReproductora { get; set; }
    public int? TipoLesionLista { get; set; }
    /// <summary>Texto del tipo de lesión ya resuelto por el origen (JSON "lesiontipotext", binding case-insensitive).</summary>
    public string? LesionTipoText { get; set; }
    public int? AveHembra { get; set; }
    public int? AveMacho { get; set; }
    public int? AveMixto { get; set; }
}

/// <summary>InfoProductivaLoteReproductora = seguimiento diario de la reproductora.</summary>
public sealed class PanamaInfoProductivaRepro
{
    public int Id { get; set; }
    public DateTime? FechaRegistro { get; set; }
    public int? EdadDia { get; set; }
    public int? MortalidadHembra { get; set; }
    public int? MortalidadMacho { get; set; }
    public int? MortalidadMixta { get; set; }
    public int? SeleccionHembra { get; set; }
    public int? SeleccionMacho { get; set; }
    public int? SeleccionMixto { get; set; }
    public double? PesoAveHembra { get; set; }
    public double? PesoAveMacho { get; set; }
    public double? PesoAveMixto { get; set; }
    public double? QqHembra { get; set; }
    public double? QqMacho { get; set; }
    public double? QqMixto { get; set; }
    public string? Observacion { get; set; }
}
