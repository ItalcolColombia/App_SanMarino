// src/ZooSanMarino.Application/Calculos/ImplementacionCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica pura del módulo Implementación (sin EF/estado): estados de plan y tarea, % de avance,
/// vencimiento, autorización de confirmación y plantilla por defecto del cronograma de entrega.
/// </summary>
public static class ImplementacionCalculos
{
    // Estados de plan
    public const string PlanBorrador   = "borrador";
    public const string PlanEnProgreso = "en_progreso";
    public const string PlanCompletado = "completado";
    public const string PlanCancelado  = "cancelado";

    // Estados de tarea
    public const string TareaPendiente  = "pendiente";
    public const string TareaCompletada = "completada";
    public const string TareaConfirmada = "confirmada";

    // Tipos de cronograma (el plan sirve para entregas y capacitaciones)
    public const string TipoImplementacion = "implementacion";
    public const string TipoCapacitacion   = "capacitacion";
    public const string TipoMixto          = "mixto";

    // Estados de firma de participante (asistente que confirma recibido)
    public const string FirmaPendiente = "pendiente";
    public const string FirmaFirmada   = "firmada";
    public const string FirmaRechazada = "rechazada";

    // Tipo de trazo con el que el participante firmó
    public const string FirmaTipoDigitada   = "digitada";
    public const string FirmaTipoManuscrita = "manuscrita";

    public sealed record ResumenPlan(
        int TotalTareas,
        int Completadas,
        int Confirmadas,
        decimal PorcentajeAvance,
        decimal PorcentajeConfirmado);

    public sealed record PlantillaTarea(string Categoria, string Titulo, int Orden);

    /// <summary>
    /// Resumen de avance. <paramref name="completadas"/> son las tareas en estado "completada"
    /// (aún sin confirmar); las confirmadas también cuentan como avance porque ya pasaron por el
    /// check. Porcentajes 0–100 redondeados a 1 decimal (AwayFromZero); total 0 → 0 %.
    /// </summary>
    public static ResumenPlan CalcularResumen(int totalTareas, int completadas, int confirmadas)
    {
        if (totalTareas <= 0)
            return new ResumenPlan(0, 0, 0, 0m, 0m);

        var avance = completadas + confirmadas;
        var pctAvance     = Math.Round(100m * avance      / totalTareas, 1, MidpointRounding.AwayFromZero);
        var pctConfirmado = Math.Round(100m * confirmadas / totalTareas, 1, MidpointRounding.AwayFromZero);
        return new ResumenPlan(totalTareas, completadas, confirmadas, pctAvance, pctConfirmado);
    }

    /// <summary>
    /// Estado derivado del plan tras un cambio en sus tareas. "cancelado" es manual y se respeta;
    /// sin tareas → borrador; todas confirmadas → completado; algún avance (completadas o
    /// confirmadas) → en_progreso; con tareas pero sin ningún check → borrador (aún en armado).
    /// <paramref name="avance"/> = completadas + confirmadas.
    /// </summary>
    public static string DeterminarEstadoPlan(string estadoActual, int totalTareas, int confirmadas, int avance)
    {
        if (estadoActual == PlanCancelado) return PlanCancelado;
        if (totalTareas <= 0) return PlanBorrador;
        if (confirmadas >= totalTareas) return PlanCompletado;
        if (avance > 0) return PlanEnProgreso;
        return PlanBorrador;
    }

    /// <summary>Vencida = tiene fecha programada anterior a hoy y sigue pendiente (sin check).</summary>
    public static bool EsTareaVencida(DateTime? fechaProgramada, DateTime hoy, string estado)
        => fechaProgramada.HasValue
           && fechaProgramada.Value.Date < hoy.Date
           && estado == TareaPendiente;

    /// <summary>
    /// Solo el usuario asignado puede confirmar, y únicamente cuando la tarea ya fue marcada
    /// completada por el gestor (doble check). Nulls → false (fail-closed).
    /// </summary>
    public static bool PuedeConfirmar(string estadoTarea, Guid? asignadoUserId, Guid? usuarioActual)
        => estadoTarea == TareaCompletada
           && asignadoUserId.HasValue
           && usuarioActual.HasValue
           && asignadoUserId.Value == usuarioActual.Value;

    /// <summary>
    /// Normaliza el tipo de cronograma: null/vacío → "implementacion" (default histórico);
    /// otro valor debe ser un tipo conocido o lanza <see cref="InvalidOperationException"/>.
    /// </summary>
    public static string NormalizarTipoPlan(string? tipo)
    {
        var t = (tipo ?? "").Trim().ToLowerInvariant();
        if (t.Length == 0) return TipoImplementacion;
        if (t is TipoImplementacion or TipoCapacitacion or TipoMixto) return t;
        throw new InvalidOperationException(
            "Tipo de cronograma inválido; use 'implementacion', 'capacitacion' o 'mixto'.");
    }

    public sealed record ResumenFirmas(
        int Total,
        int Firmadas,
        int Rechazadas,
        int Pendientes,
        decimal PorcentajeFirmado);

    /// <summary>
    /// Resumen de firmas de participantes. Pendientes = total − firmadas − rechazadas (mínimo 0).
    /// Porcentaje 0–100 con 1 decimal (AwayFromZero, igual que los % de avance); total 0 → 0 %.
    /// </summary>
    public static ResumenFirmas CalcularResumenFirmas(int total, int firmadas, int rechazadas)
    {
        if (total <= 0)
            return new ResumenFirmas(0, 0, 0, 0, 0m);

        var pendientes = Math.Max(0, total - firmadas - rechazadas);
        var pct = Math.Round(100m * firmadas / total, 1, MidpointRounding.AwayFromZero);
        return new ResumenFirmas(total, firmadas, rechazadas, pendientes, pct);
    }

    /// <summary>Firmar: vale desde pendiente y también desde rechazada (el participante se retracta de la novedad).</summary>
    public static bool PuedeFirmar(string estadoFirma)
        => estadoFirma is FirmaPendiente or FirmaRechazada;

    /// <summary>Rechazar (novedad): solo desde pendiente; una firma ya digitada no se puede rechazar.</summary>
    public static bool PuedeRechazar(string estadoFirma)
        => estadoFirma == FirmaPendiente;

    /// <summary>
    /// Valida y normaliza la firma digitada: obligatoria, 3–300 caracteres tras trim.
    /// Devuelve el texto normalizado o lanza <see cref="InvalidOperationException"/>.
    /// </summary>
    public static string ValidarFirmaTexto(string? firma)
    {
        var f = (firma ?? "").Trim();
        if (f.Length < 3)
            throw new InvalidOperationException("La firma es obligatoria (escribí tu nombre completo, mínimo 3 caracteres).");
        if (f.Length > 300)
            throw new InvalidOperationException("La firma supera el máximo de 300 caracteres.");
        return f;
    }

    /// <summary>
    /// El participante recién puede firmar cuando el encargado dio por terminado el punto
    /// (<c>completada</c>) — o cuando ya se confirmó. Mientras la tarea siga <c>pendiente</c> el
    /// punto se ve como "programado" y la firma queda cerrada: se firma lo que ya se hizo, no lo
    /// que está por hacerse. Estado desconocido → false (fail-closed).
    /// </summary>
    public static bool TareaHabilitadaParaFirmar(string? estadoTarea)
        => estadoTarea is TareaCompletada or TareaConfirmada;

    /// <summary>
    /// Longitud máxima del PNG en base64 de la firma manuscrita (~700 KB de data URL ≈ 512 KB de
    /// imagen). Un canvas de 600×200 comprimido pesa 10–25 KB: el tope solo frena payloads absurdos.
    /// </summary>
    public const int FirmaImagenMaxChars = 700_000;

    /// <summary>
    /// Valida y normaliza el trazo manuscrito: data URL PNG (<c>data:image/png;base64,…</c>) con
    /// contenido real. Devuelve la data URL normalizada, o null si no se envió trazo (el llamador
    /// decide si eso es válido según el tipo de firma).
    /// </summary>
    public static string? ValidarFirmaImagen(string? imagen)
    {
        var img = (imagen ?? "").Trim();
        if (img.Length == 0) return null;

        const string prefijo = "data:image/png;base64,";
        if (!img.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La firma debe ser una imagen PNG (data:image/png;base64,…).");
        if (img.Length > FirmaImagenMaxChars)
            throw new InvalidOperationException("La firma dibujada es demasiado pesada; volvé a firmarla.");

        var payload = img[prefijo.Length..];
        // Un canvas en blanco produce un PNG muy chico: exigimos trazo real, no una hoja vacía.
        if (payload.Length < 200)
            throw new InvalidOperationException("La firma está vacía; dibujá tu firma antes de aceptar.");
        if (!IsBase64(payload))
            throw new InvalidOperationException("La firma dibujada llegó corrupta; volvé a firmarla.");

        return prefijo + payload;
    }

    private static bool IsBase64(string s)
    {
        Span<byte> buffer = new byte[s.Length];
        return Convert.TryFromBase64String(s, buffer, out _);
    }

    /// <summary>
    /// Huella de LO QUE SE FIRMÓ (plan + punto + fecha en que se dio por realizado). Se guarda junto
    /// a la firma para que, si alguien edita el título o la descripción del punto después, el detalle
    /// pueda mostrar que el contenido cambió respecto de lo aceptado. Sin esto una firma manuscrita
    /// prueba menos que la digitada: sería una imagen sin objeto.
    /// </summary>
    /// <remarks>
    /// Los campos van separados por <c>\n</c> con etiqueta fija, de modo que reordenar o renombrar
    /// una propiedad del DTO no cambie el hash de firmas viejas. Se calcula SIEMPRE en el servidor.
    /// </remarks>
    public static string CalcularContenidoHash(
        string planNombre, string categoria, string titulo, string? descripcion, DateTime? fechaCompletada)
    {
        var canon = string.Join('\n',
            "plan="        + (planNombre ?? "").Trim(),
            "categoria="   + (categoria  ?? "").Trim(),
            "titulo="      + (titulo     ?? "").Trim(),
            "descripcion=" + (descripcion ?? "").Trim(),
            "completada="  + (fechaCompletada?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") ?? ""));

        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canon));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Checklist estándar de entrega de la aplicación, usado al crear un plan con "usar plantilla".
    /// El orden es global (1..n) para que el cronograma quede secuenciado entre categorías.
    /// </summary>
    public static IReadOnlyList<PlantillaTarea> PlantillaPorDefecto() => new[]
    {
        new PlantillaTarea("Parametrizaciones", "Parametrización de empresa, país y usuarios",          1),
        new PlantillaTarea("Parametrizaciones", "Parametrización de granjas, núcleos y galpones",       2),
        new PlantillaTarea("Parametrizaciones", "Parametrización de catálogos e inventario",            3),
        new PlantillaTarea("Parametrizaciones", "Parametrización de guías genéticas",                   4),
        new PlantillaTarea("Capacitación",      "Capacitación en módulos de configuración",             5),
        new PlantillaTarea("Capacitación",      "Capacitación en seguimiento diario",                   6),
        new PlantillaTarea("Capacitación",      "Capacitación en reportes y liquidaciones",             7),
        new PlantillaTarea("Carga de datos",    "Carga de datos iniciales (lotes activos)",             8),
        new PlantillaTarea("Carga de datos",    "Validación de datos migrados con el usuario",          9),
        new PlantillaTarea("Puesta en marcha",  "Acompañamiento primera semana de operación",          10),
        new PlantillaTarea("Puesta en marcha",  "Acta de entrega y cierre de implementación",          11),
    };
}
