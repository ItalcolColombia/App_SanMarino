namespace ZooSanMarino.Application.Calculos;

/// <summary>Qué hacer con el alta de un seguimiento de producción cuando ya hay algo ese día.</summary>
public enum AltaSeguimientoDelDia
{
    /// <summary>Se crea una fila nueva.</summary>
    Insertar,

    /// <summary>Se suma sobre la fila que dejó el arrastre de huevos del levante.</summary>
    MergearSobreArrastre,

    /// <summary>Se rechaza: la empresa admite un solo registro por lote y por día.</summary>
    Rechazar
}

/// <summary>
/// Decide si un lote puede tener más de un seguimiento diario el mismo día, según el flag de empresa
/// <c>companies.permite_multiples_seguimientos_diarios</c>.
///
/// <para>
/// <b>Por qué existe.</b> El flag se podía encender desde la pantalla de Empresas, pero producción
/// seguía rechazando el segundo registro del día en tres lugares que nunca lo leyeron: el alta y la
/// edición de <c>ProduccionService</c>, y la edición de levante. Con el flag apagado cada decisión de
/// acá devuelve exactamente lo que el código hacía antes; con el flag encendido deja pasar el
/// segundo registro. El trigger de BD <c>fn_trg_seguimiento_unico_por_dia</c> aplica la misma regla
/// del lado de la base.
/// </para>
///
/// <para>
/// Solo cambia <b>levante</b> y <b>producción</b>. Reproductora (y el tipo legacy <c>produccion</c> de
/// la tabla de levante) sigue con un registro por día aunque la empresa tenga el flag.
/// </para>
/// </summary>
public static class SeguimientoVariosPorDiaCalculos
{
    /// <summary>
    /// Alta de producción. La fila del arrastre de huevos del levante se sigue mergeando con o sin
    /// flag, porque no es un registro del usuario sino el lugar donde van sus huevos de ese día.
    /// </summary>
    public static AltaSeguimientoDelDia ResolverAltaProduccion(
        bool hayRegistroDelDia, bool esFilaDeArrastre, bool permiteMultiples)
    {
        if (!hayRegistroDelDia) return AltaSeguimientoDelDia.Insertar;
        if (esFilaDeArrastre) return AltaSeguimientoDelDia.MergearSobreArrastre;
        return permiteMultiples ? AltaSeguimientoDelDia.Insertar : AltaSeguimientoDelDia.Rechazar;
    }

    /// <summary>
    /// Edición de producción: ¿se rechaza porque ya hay OTRO registro del mismo lote ese día?
    /// </summary>
    public static bool RechazaEdicionProduccion(bool hayOtroRegistroDelDia, bool permiteMultiples) =>
        hayOtroRegistroDelDia && !permiteMultiples;

    /// <summary>
    /// Tabla de levante: ¿rige «un registro por día» para este tipo de seguimiento? Solo el tipo
    /// <c>levante</c> de una empresa con el flag queda libre.
    /// </summary>
    public static bool AplicaUnicoPorDiaLevante(string? tipoSeguimiento, bool permiteMultiples) =>
        !(permiteMultiples && string.Equals(tipoSeguimiento, "levante", StringComparison.Ordinal));
}
