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
    public void SeguimientoReproductoraEngorde_SoloFechaEsRequeridaEnEncabezados()
    {
        // "Reproductora" es opcional a nivel encabezado: la obligatoriedad por CELDA la aplica el
        // parser (salvo que se elija una reproductora en pantalla). Lote/Granja/Núcleo/Galpón idem.
        var requeridas = MigracionEsquemas.SeguimientoReproductoraEngorde.Columnas
            .Where(c => c.Requerida).Select(c => c.Titulo).ToList();
        Assert.Equal(new[] { "Fecha" }, requeridas);
        Assert.Contains(MigracionEsquemas.SeguimientoReproductoraEngorde.Columnas, c => c.Titulo == "Reproductora");
        Assert.Contains(MigracionEsquemas.SeguimientoReproductoraEngorde.Columnas, c => c.Titulo == "Lote");
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

    [Fact]
    public void VentaPolloEngorde_ArchivoConLas11ColumnasViejas_SigueSiendoValido()
    {
        // Compatibilidad hacia atrás: la plantilla de venta pasó de 11 a 26 columnas (ubicación
        // multi-lote + datos de despacho + Estado + Venta sobre mixtas). Un archivo generado con la
        // plantilla anterior no debe reportar faltantes ni desconocidos.
        var esquema = MigracionEsquemas.VentaPolloEngorde;
        var viejas = new[]
        {
            "Fecha", "Cantidad H", "Cantidad M", "Cantidad Mixtas", "Motivo",
            "Peso Bruto (kg)", "Peso Tara (kg)", "Edad Aves", "Raza", "Placa", "Observaciones"
        }.Select(MigracionCalculos.NormalizarClave).ToList();

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, viejas);

        Assert.Empty(faltantes);
        Assert.Empty(desconocidos);
    }

    [Fact]
    public void VentaPolloEngorde_SoloFechaEsRequerida()
    {
        // El lote sale del contexto de pantalla o de la columna "Lote": nada más es obligatorio
        // a nivel encabezado (las reglas por celda las aplica el parser).
        var requeridas = MigracionEsquemas.VentaPolloEngorde.Columnas
            .Where(c => c.Requerida).Select(c => c.Titulo).ToList();
        Assert.Equal(new[] { "Fecha" }, requeridas);
    }

    [Fact]
    public void VentaPolloEngorde_TieneLasColumnasDelFormularioDeVenta()
    {
        // Gate del pedido: la carga masiva debe cubrir TODOS los campos que se usan al vender.
        var titulos = MigracionEsquemas.VentaPolloEngorde.Columnas.Select(c => c.Titulo).ToList();
        foreach (var esperada in new[]
        {
            "Granja", "Núcleo", "Galpón", "Lote",
            "N° Despacho", "Total Pollos Galpón", "Hora Salida", "Guía Agrocalidad",
            "Sellos", "Ayuno", "Cliente / Conductor", "Planta Destino", "Descripción",
            "Estado", "Venta sobre mixtas"
        })
            Assert.Contains(esperada, titulos);
    }

    [Fact]
    public void VentaPolloEngorde_EstadoOfreceCompletadoYPendiente()
    {
        var estado = MigracionEsquemas.VentaPolloEngorde.Columnas.Single(c => c.Titulo == "Estado");
        Assert.Equal(new[] { "Completado", "Pendiente" }, estado.Opciones);
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

    // ── Seguimiento pollo engorde MIXTO (Panamá) ─────────────────────────────
    // La plantilla de una empresa con seguimiento_engorde_mixto = true emite títulos "Mixta/Mixto",
    // pero el archivo se parsea SIEMPRE con el esquema por sexo. Estos tests son el contrato entre
    // los dos: si alguien renombra un título mixto sin actualizar el alias, el archivo que el propio
    // sistema genera dejaría de cargar su columna (en silencio, con una simple advertencia).

    [Fact]
    public void PlantillaMixta_TodosSusTitulosLosAceptaElEsquemaDeParseo()
    {
        var titulosMixtos = MigracionEsquemas.SeguimientoPolloEngordeMixto.Columnas
            .Select(c => c.Titulo).ToList();

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(
            MigracionEsquemas.SeguimientoPolloEngorde, titulosMixtos);

        Assert.Empty(desconocidos);
        Assert.Empty(faltantes); // trae Fecha, la única requerida
    }

    [Theory]
    [InlineData("Mort Mixta")]
    [InlineData("mortalidad mixta")]
    [InlineData("Sel Mixta")]
    [InlineData("Consumo Mixto (kg)")]
    [InlineData("consumo mixto")]
    [InlineData("Peso Mixto (g)")]
    [InlineData("Uniformidad Mixta")]
    [InlineData("Alimento 1 Mixto")]
    [InlineData("Consumo Alimento 1 Mixto")]
    public void EncabezadoMixto_NoEsDesconocidoParaElParser(string encabezado)
    {
        var (_, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(
            MigracionEsquemas.SeguimientoPolloEngorde, new[] { "Fecha", encabezado });

        Assert.Empty(desconocidos);
    }

    [Fact]
    public void PlantillaMixta_NoTieneColumnasPorSexo()
    {
        var titulos = MigracionEsquemas.SeguimientoPolloEngordeMixto.Columnas.Select(c => c.Titulo);

        Assert.DoesNotContain(titulos, t => t.EndsWith(" H") || t.EndsWith(" M")
                                         || t.Contains("H (") || t.Contains("M ("));
    }

    [Fact]
    public void EncabezadosPorSexo_SiguenCargandoIgual_Regresion()
    {
        // Un archivo generado antes de la variante mixta no debe perder ninguna columna.
        var titulosPorSexo = MigracionEsquemas.SeguimientoPolloEngorde.Columnas
            .Select(c => c.Titulo).ToList();

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(
            MigracionEsquemas.SeguimientoPolloEngorde, titulosPorSexo);

        Assert.Empty(desconocidos);
        Assert.Empty(faltantes);
    }
}
