using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Esquema único por tipo de migración (F1): consistencia estructural de los 9 esquemas y el cálculo
/// puro de validación de encabezados / cap de errores (<see cref="MigracionEsquemaCalculos"/>).
/// </summary>
public class MigracionEsquemasTests
{
    public static IEnumerable<object[]> Tipos =>
        MigracionEsquemas.TiposConEsquema.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Tipos))]
    public void Para_DevuelveEsquemaConHojaDatosYMaxFilas5000(TipoMigracion tipo)
    {
        var esquema = MigracionEsquemas.Para(tipo);
        Assert.Equal("Datos", esquema.Hoja);
        Assert.Equal(5000, esquema.MaxFilas);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public void Para_TieneAlMenosUnaColumnaRequerida(TipoMigracion tipo)
    {
        var esquema = MigracionEsquemas.Para(tipo);
        Assert.Contains(esquema.Columnas, c => c.Requerida);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public void Para_TitulosNormalizadosUnicos(TipoMigracion tipo)
    {
        var esquema = MigracionEsquemas.Para(tipo);
        var normalizados = esquema.Columnas.Select(c => MigracionCalculos.NormalizarClave(c.Titulo)).ToList();
        Assert.Equal(normalizados.Count, normalizados.Distinct().Count());
    }

    [Fact]
    public void Para_TipoSinEsquema_Lanza()
        => Assert.Throws<NotSupportedException>(() => MigracionEsquemas.Para(TipoMigracion.Ventas));

    [Fact]
    public void SeguimientoReproductoraEngorde_RequiereReproductoraYFecha()
    {
        var requeridas = MigracionEsquemas.SeguimientoReproductoraEngorde.Columnas
            .Where(c => c.Requerida).Select(c => c.Titulo).ToList();
        Assert.Equal(new[] { "Reproductora", "Fecha" }, requeridas);
    }

    [Fact]
    public void SeguimientoPolloEngorde_ArchivoSinColumnasQq_SigueSiendoValido()
    {
        // Compatibilidad hacia atrás: las columnas QQ (Panamá) son opcionales; un archivo generado
        // con la plantilla anterior (sin QQ) no debe reportar faltantes.
        var esquema = MigracionEsquemas.SeguimientoPolloEngorde;
        var headersSinQq = esquema.Columnas
            .Where(c => !c.Titulo.StartsWith("QQ"))
            .Select(c => MigracionCalculos.NormalizarClave(c.Titulo)).ToList();

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, headersSinQq);

        Assert.Empty(faltantes);
        Assert.Empty(desconocidos);
    }

    // ── ValidarEncabezados ────────────────────────────────────────────────────

    [Fact]
    public void ValidarEncabezados_TodasLasColumnasPresentes_SinFaltantes()
    {
        var esquema = MigracionEsquemas.Nucleos; // Granja, Código Núcleo, Nombre (todas requeridas)
        var headers = esquema.Columnas.Select(c => MigracionCalculos.NormalizarClave(c.Titulo)).ToList();

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, headers);

        Assert.Empty(faltantes);
        Assert.Empty(desconocidos);
    }

    [Fact]
    public void ValidarEncabezados_FaltaRequerida_LaReporta()
    {
        var esquema = MigracionEsquemas.Nucleos;
        var headers = new[] { "granja", "nombre" }; // falta "Código Núcleo"

        var (faltantes, _) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, headers);

        Assert.Single(faltantes);
        Assert.Equal("Código Núcleo", faltantes[0]);
    }

    [Fact]
    public void ValidarEncabezados_AliasEnLugarDelTitulo_Satisface()
    {
        var esquema = MigracionEsquemas.Nucleos;
        // "codigo" es alias de "Código Núcleo"
        var headers = new[] { "granja", "codigo", "nombre" };

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, headers);

        Assert.Empty(faltantes);
        Assert.Empty(desconocidos);
    }

    [Fact]
    public void ValidarEncabezados_HeaderDesconocido_SeReporta()
    {
        var esquema = MigracionEsquemas.Nucleos;
        var headers = new[] { "granja", "codigo nucleo", "nombre", "columna inventada" };

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, headers);

        Assert.Empty(faltantes);
        Assert.Single(desconocidos);
        Assert.Equal("columna inventada", desconocidos[0]);
    }

    [Fact]
    public void ValidarEncabezados_CaseYAcentosDistintos_Matchean()
    {
        var esquema = MigracionEsquemas.Nucleos;
        // Headers "sin normalizar" (mayúsculas/acentos) — la función normaliza igual que NormalizarClave.
        var headers = new[] { "GRANJA", "Código Núcleo", "  Nombre  " };

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, headers);

        Assert.Empty(faltantes);
        Assert.Empty(desconocidos);
    }

    [Fact]
    public void ValidarEncabezados_OrdenDistintoNoAfecta()
    {
        var esquema = MigracionEsquemas.Galpones;
        var headersEnOrden = esquema.Columnas.Select(c => MigracionCalculos.NormalizarClave(c.Titulo)).ToList();
        var headersInvertidos = headersEnOrden.AsEnumerable().Reverse().ToList();

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, headersInvertidos);

        Assert.Empty(faltantes);
        Assert.Empty(desconocidos);
    }

    [Fact]
    public void ValidarEncabezados_ColumnaOpcionalFaltante_NoSeReportaComoFaltante()
    {
        // Galpones: "Código Galpón" es opcional (Requerida=false).
        var esquema = MigracionEsquemas.Galpones;
        var headers = new[] { "granja", "nucleo", "nombre" }; // sin código galpón, ancho, largo, tipo galpon

        var (faltantes, _) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, headers);

        Assert.Empty(faltantes);
    }

    // ── LimitarErrores ──────────────────────────────────────────────────────

    [Fact]
    public void LimitarErrores_NMenorIgualQueMax_QuedaIntacta()
    {
        var errores = Enumerable.Range(1, 5)
            .Select(i => new MigracionErrorDto(i, "Col", null, $"Error {i}"))
            .ToList();

        var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(errores, 10);

        Assert.Equal(5, capados.Count);
        Assert.Equal(5, totalReal);
        Assert.Same(errores, capados);
    }

    [Fact]
    public void LimitarErrores_NMayorQueMax_CapaYAgregaMeta()
    {
        var errores = Enumerable.Range(1, 12)
            .Select(i => new MigracionErrorDto(i, "Col", null, $"Error {i}"))
            .ToList();

        var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(errores, 10);

        Assert.Equal(11, capados.Count); // 10 + 1 meta
        Assert.Equal(12, totalReal);
        Assert.Equal("Advertencia", capados[^1].Severidad);
        Assert.Contains("primeros 10 de 12", capados[^1].Mensaje);
    }

    [Fact]
    public void LimitarErrores_ListaVacia_QuedaVacia()
    {
        var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(Array.Empty<MigracionErrorDto>(), 10);

        Assert.Empty(capados);
        Assert.Equal(0, totalReal);
    }
}
