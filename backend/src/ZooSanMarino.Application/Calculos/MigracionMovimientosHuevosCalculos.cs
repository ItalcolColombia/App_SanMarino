// src/ZooSanMarino.Application/Calculos/MigracionMovimientosHuevosCalculos.cs
// Reglas PURAS de la hoja "Movimientos Huevos" de la carga masiva de Seguimiento PRODUCCIÓN.
//
// La hoja registra salidas de huevos del lote del archivo hacia PLANTA (Traslado) o VENTA, contra
// `traslado_huevos` en estado Completado + UN recálculo absoluto del espejo al final (patrón de la
// spec de Fase 3). Nunca toca `seguimiento_diario_levante`: ahí vive el trigger del espejo y el
// descuento se aplicaría dos veces.
namespace ZooSanMarino.Application.Calculos;

/// <summary>Operación aceptada por la hoja "Movimientos Huevos" (espejo de traslado_huevos.tipo_operacion).</summary>
public enum MovimientoHuevosMigracion
{
    /// <summary>Traslado de huevos (típicamente a planta).</summary>
    Traslado,
    /// <summary>Venta de huevos (cliente/empresa/planta).</summary>
    Venta
}

public static class MigracionMovimientosHuevosCalculos
{
    /// <summary>Opciones válidas de "Tipo Destino" (las mismas de la UI de traslados de huevos).</summary>
    public static readonly string[] TiposDestino = { "Planta", "Cliente", "Empresa" };

    /// <summary>
    /// Interpreta la celda "Tipo" (sin acentos ni mayúsculas). Celda vacía o texto no reconocido ⇒
    /// false — no hay default: la operación define venta vs traslado.
    /// </summary>
    public static bool TryOperacion(string? texto, out MovimientoHuevosMigracion tipo)
    {
        tipo = default;
        switch (MigracionCalculos.NormalizarClave(texto))
        {
            case "traslado":
            case "traslados":
            case "traslado a planta":
            case "planta":
            case "envio a planta":
                tipo = MovimientoHuevosMigracion.Traslado;
                return true;

            case "venta":
            case "ventas":
            case "venta de huevos":
            case "venta huevos":
                tipo = MovimientoHuevosMigracion.Venta;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// "Tipo Destino" efectivo: el de la celda si es válido; vacío ⇒ el default de la UI
    /// (Traslado ⇒ Planta, Venta ⇒ Cliente). Texto no reconocido ⇒ null (error de fila).
    /// </summary>
    public static string? TipoDestinoEfectivo(string? texto, MovimientoHuevosMigracion tipo)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return tipo == MovimientoHuevosMigracion.Traslado ? "Planta" : "Cliente";
        var clave = MigracionCalculos.NormalizarClave(texto);
        foreach (var opcion in TiposDestino)
            if (MigracionCalculos.NormalizarClave(opcion) == clave) return opcion;
        return null;
    }

    /// <summary>
    /// Clave de duplicado (archivo y reimport): fecha + operación + las 11 cantidades. Igual criterio
    /// que la hoja de aves: dos movimientos idénticos el mismo día van como una sola fila sumada.
    /// </summary>
    public static string ClaveArchivo(DateTime fecha, MovimientoHuevosMigracion tipo, HuevosClasificacion c) =>
        FormattableString.Invariant(
            $"{fecha:yyyy-MM-dd}|{tipo}|{c.Limpio}|{c.Tratado}|{c.Sucio}|{c.Deforme}|{c.Blanco}|{c.DobleYema}|{c.Piso}|{c.Pequeno}|{c.Roto}|{c.Desecho}|{c.Otro}");
}
