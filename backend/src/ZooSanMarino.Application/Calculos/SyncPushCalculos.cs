// src/ZooSanMarino.Application/Calculos/SyncPushCalculos.cs
// Reglas PURAS del push offline (PWA F3). Sin EF, sin contexto, sin reloj propio.
// El service resuelve datos (empresas del usuario, operaciones ya vistas) y delega la decisión acá.

namespace ZooSanMarino.Application.Calculos;

public static class SyncPushCalculos
{
    /// <summary>
    /// Tope de operaciones por lote. Un lote más grande se rechaza entero y tipado: truncar en
    /// silencio haría que el dispositivo diera por enviadas operaciones que el servidor nunca vio.
    /// </summary>
    public const int MaxOperacionesPorLote = 25;

    /// <summary>
    /// Tolerancia de reloj del dispositivo. Una tablet de granja puede estar corrida; 24 h separa
    /// "reloj desajustado" de "fecha inventada".
    /// </summary>
    public static readonly TimeSpan ToleranciaRelojFuturo = TimeSpan.FromHours(24);

    /// <summary>
    /// Códigos ESTABLES. El cliente decide con esto (bandeja vs reintento), así que cambiarlos rompe
    /// dispositivos ya instalados. Nunca se manda un mensaje libre en español en su lugar.
    /// </summary>
    public static class Errores
    {
        public const string Validacion = "validacion";
        public const string EmpresaNoAutorizada = "empresa_no_autorizada";
        public const string ContratoObsoleto = "contrato_obsoleto";
        public const string DuplicadoEnLote = "duplicado_en_lote";
        public const string LoteExcedido = "lote_excedido";
        public const string ReglaDeNegocio = "regla_de_negocio";
        public const string ErrorInterno = "error_interno";
    }

    public static class Estados
    {
        public const string Aplicada = "aplicada";
        public const string Rechazada = "rechazada";

        /// <summary>
        /// Clase (b) del plan: divergencia con el mundo (stock/aves insuficientes). Se aplica igual y
        /// se marca para cuadre, porque perder el dato de campo es peor que un saldo negativo
        /// temporal. **Todavía sin emisor**: el alta de levante no valida saldos. Modelado acá para
        /// que F3.2 no tenga que migrar la tabla.
        /// </summary>
        public const string RequiereCuadre = "requiere_cuadre";
    }

    public static class Tipos
    {
        public const string SeguimientoLevanteCrear = "seguimiento_levante_crear";
        public const string SeguimientoProduccionCrear = "seguimiento_produccion_crear";
        public const string SeguimientoEngordeCrear = "seguimiento_engorde_crear";
        public const string SeguimientoReproductoraEngordeCrear = "seguimiento_reproductora_engorde_crear";

        /// <summary>
        /// Las CUATRO superficies de captura diaria del sistema: postura (levante + producción) y
        /// engorde (pollo + su reproductora). Un tipo fuera de esta lista se rechaza como contrato
        /// obsoleto, nunca se adivina a qué service iba.
        /// </summary>
        public static readonly IReadOnlyCollection<string> Todos =
        [
            SeguimientoLevanteCrear,
            SeguimientoProduccionCrear,
            SeguimientoEngordeCrear,
            SeguimientoReproductoraEngordeCrear
        ];

        public static bool EsConocido(string? tipo) => tipo is not null && Todos.Contains(tipo);
    }

    public enum Decision
    {
        /// <summary>Aplicar el efecto.</summary>
        Procesar,
        /// <summary>Rechazo sin tocar la base.</summary>
        Rechazar
    }

    public sealed record Veredicto(Decision Decision, string? ErrorCodigo, string? Detalle)
    {
        public static Veredicto Procesar() => new(Decision.Procesar, null, null);
        public static Veredicto Rechazar(string codigo, string detalle) => new(Decision.Rechazar, codigo, detalle);
    }

    /// <summary>
    /// ¿El lote entero es admisible? Se evalúa ANTES de tocar nada: un lote sobredimensionado no
    /// procesa "las primeras 25".
    /// </summary>
    public static string? ValidarLote(int cantidadOperaciones)
    {
        if (cantidadOperaciones <= 0)
        {
            return Errores.Validacion;
        }

        return cantidadOperaciones > MaxOperacionesPorLote ? Errores.LoteExcedido : null;
    }

    /// <summary>
    /// Veredicto de UNA operación, antes de aplicar su efecto.
    /// </summary>
    /// <param name="clientOpId">Tal como llegó (texto), para poder rechazar un uuid mal formado como
    /// error de captura en vez de romper el binding del lote entero.</param>
    /// <param name="empresasDelUsuario">Empresas que el JWT habilita. Vacío ⇒ nada se aplica.</param>
    /// <param name="clientOpIdsYaVistosEnLote">Ids ya procesados en este mismo lote.</param>
    /// <param name="empresaDeLaSesion">
    /// Empresa efectiva de ESTA petición. Los services aguas abajo filtran el lote por ella
    /// (<c>l.CompanyId == _current.CompanyId</c>), así que una operación de otra empresa no se
    /// aplicaría igual — pero fallaría con un "Lote no existe" que en campo se lee como un dato
    /// corrupto. Se rechaza acá, con el motivo real. <c>null</c> omite la comprobación.
    /// </param>
    public static Veredicto EvaluarOperacion(
        string? clientOpId,
        string? tipo,
        int companyId,
        DateTime? capturadoAtDispositivo,
        DateTime ahoraUtc,
        IReadOnlyCollection<int> empresasDelUsuario,
        IReadOnlyCollection<string> clientOpIdsYaVistosEnLote,
        int? empresaDeLaSesion = null)
    {
        if (string.IsNullOrWhiteSpace(clientOpId))
        {
            return Veredicto.Rechazar(Errores.Validacion, "La operación no trae clientOpId.");
        }

        if (!Guid.TryParse(clientOpId.Trim(), out var parsed) || parsed == Guid.Empty)
        {
            return Veredicto.Rechazar(Errores.Validacion, "clientOpId no es un UUID válido.");
        }

        // Dos veces el mismo id en el mismo lote: la segunda no se procesa. Sin esto, el UNIQUE de la
        // base haría fallar la transacción entera y se perderían las operaciones sanas del lote.
        if (clientOpIdsYaVistosEnLote.Any(x => string.Equals(x, parsed.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return Veredicto.Rechazar(Errores.DuplicadoEnLote, "El lote trae dos veces el mismo clientOpId.");
        }

        if (!Tipos.EsConocido(tipo))
        {
            // El dispositivo tiene una versión más nueva (o más vieja) que este servidor.
            return Veredicto.Rechazar(Errores.ContratoObsoleto, $"Tipo de operación no soportado: '{tipo}'.");
        }

        // Fail-closed: el 0 es el hueco que deja un campo sin llenar, no una empresa. Aceptarlo y
        // "caer a la del token" es exactamente el fallback silencioso que hace que un outbox
        // reproducido escriba en la empresa equivocada con 200 OK.
        if (companyId <= 0)
        {
            return Veredicto.Rechazar(Errores.EmpresaNoAutorizada, "La operación no declara empresa.");
        }

        if (!empresasDelUsuario.Contains(companyId))
        {
            return Veredicto.Rechazar(
                Errores.EmpresaNoAutorizada,
                $"El usuario no tiene acceso a la empresa {companyId} al momento de sincronizar.");
        }

        if (empresaDeLaSesion is { } sesion && sesion > 0 && sesion != companyId)
        {
            return Veredicto.Rechazar(
                Errores.EmpresaNoAutorizada,
                $"La operación es de la empresa {companyId} y la sesión está en la {sesion}. " +
                "Cambiá de empresa para enviarla.");
        }

        if (capturadoAtDispositivo is { } capturado &&
            capturado.ToUniversalTime() > ahoraUtc.Add(ToleranciaRelojFuturo))
        {
            return Veredicto.Rechazar(Errores.Validacion, "La captura declara una fecha futura.");
        }

        return Veredicto.Procesar();
    }

    /// <summary>
    /// Traduce una excepción de regla de negocio del service a un resultado tipado.
    /// Los services del repo lanzan <see cref="InvalidOperationException"/> con texto en español;
    /// ese texto viaja como <c>detalle</c> (para que el galponero entienda), pero la DECISIÓN del
    /// cliente se toma con el código.
    /// </summary>
    public static Veredicto DesdeReglaDeNegocio(string? mensaje) =>
        Veredicto.Rechazar(Errores.ReglaDeNegocio, string.IsNullOrWhiteSpace(mensaje) ? "Regla de negocio." : mensaje);
}
