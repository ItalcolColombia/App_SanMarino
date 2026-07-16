using Microsoft.Extensions.Configuration;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Configuración del puente (sección "PuentePanama" de appsettings). Las credenciales son de
/// SOLO LECTURA contra el API origen; no se hardcodean, se leen de configuración.
/// </summary>
public sealed class PuentePanamaOptions
{
    public const string SeccionConfig = "PuentePanama";

    public string BaseUrl { get; set; } = "https://italapp.italcol.com/ZooPanamaPollo";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>Raza global a asignar a los lotes migrados (override). Vacío = usar la línea genética que traiga cada lote del origen.</summary>
    public string GeneticaRaza { get; set; } = "";

    /// <summary>Año de tabla genética a asignar. Null = usar el año de inicio de cada lote.</summary>
    public int? GeneticaAnio { get; set; }

    /// <summary>Nombre de la empresa destino esperada (guard). Vacío = sin verificación.</summary>
    public string EmpresaDestino { get; set; } = "ItalcolPanama";

    /// <summary>Departamento por defecto para granjas cuyo cliente origen no resuelva por nombre. Null = primer departamento del país Panamá.</summary>
    public int? DepartamentoId { get; set; }

    /// <summary>Municipio por defecto (debe pertenecer al departamento por defecto). Null = primer municipio del departamento resuelto.</summary>
    public int? MunicipioId { get; set; }

    /// <summary>Lee la sección de configuración por indexador (sin depender de Configuration.Binder).</summary>
    public static PuentePanamaOptions FromConfig(IConfiguration config)
    {
        var s = config.GetSection(SeccionConfig);
        var opt = new PuentePanamaOptions();
        if (!string.IsNullOrWhiteSpace(s["BaseUrl"])) opt.BaseUrl = s["BaseUrl"]!;
        opt.Email = s["Email"] ?? "";
        opt.Password = s["Password"] ?? "";
        opt.GeneticaRaza = s["GeneticaRaza"] ?? "";
        opt.GeneticaAnio = int.TryParse(s["GeneticaAnio"], out var a) ? a : null;
        if (s["EmpresaDestino"] is not null) opt.EmpresaDestino = s["EmpresaDestino"]!;
        opt.DepartamentoId = int.TryParse(s["DepartamentoId"], out var d) ? d : null;
        opt.MunicipioId = int.TryParse(s["MunicipioId"], out var m) ? m : null;
        return opt;
    }
}
