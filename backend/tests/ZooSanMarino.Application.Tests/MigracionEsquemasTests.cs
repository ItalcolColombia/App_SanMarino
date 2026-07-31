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

    // ── POSTURA: retro-compatibilidad de los archivos ya en uso ───────────────
    // Es el criterio de aceptación #1 del plan: un Excel con los encabezados históricos tiene que
    // seguir validando sin faltantes ni desconocidos después de ampliar el esquema.

    /// <summary>Las 15 columnas de la plantilla de Levante anterior a la ampliación (alimento + huevos).</summary>
    private static readonly string[] LevanteViejas =
    {
        "Fecha", "Mort H", "Mort M", "Sel H", "Sel M", "Error Sexaje H", "Error Sexaje M",
        "Consumo H (kg)", "Consumo M (kg)", "Tipo Alimento", "Peso H (g)", "Peso M (g)",
        "Uniformidad H", "Uniformidad M", "Observaciones"
    };

    /// <summary>Las 12 columnas de la plantilla de Producción anterior a la ampliación.</summary>
    private static readonly string[] ProduccionViejas =
    {
        "Fecha", "Mort H", "Mort M", "Sel H", "Sel M", "Consumo H (kg)", "Consumo M (kg)",
        "Huevo Total", "Huevo Incubable", "Peso Huevo (g)", "Etapa", "Observaciones"
    };

    [Fact]
    public void SeguimientoLevante_ArchivoConLas15ColumnasViejas_SigueSiendoValido()
    {
        var headers = LevanteViejas.Select(MigracionCalculos.NormalizarClave).ToList();

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(
            MigracionEsquemas.SeguimientoLevante, headers);

        Assert.Empty(faltantes);
        Assert.Empty(desconocidos);
    }

    [Fact]
    public void SeguimientoProduccion_ArchivoConLas12ColumnasViejas_SigueSiendoValido()
    {
        var headers = ProduccionViejas.Select(MigracionCalculos.NormalizarClave).ToList();

        var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(
            MigracionEsquemas.SeguimientoProduccion, headers);

        Assert.Empty(faltantes);
        Assert.Empty(desconocidos);
    }

    [Theory]
    [InlineData(nameof(MigracionEsquemas.SeguimientoLevante))]
    [InlineData(nameof(MigracionEsquemas.SeguimientoProduccion))]
    public void Postura_ConservaElOrdenDeLasColumnasHistoricas(string cual)
    {
        // El orden importa: los operarios copian y pegan bloques enteros de sus planillas sobre la
        // plantilla. Las columnas nuevas van SIEMPRE al final.
        var (esquema, viejas) = cual == nameof(MigracionEsquemas.SeguimientoLevante)
            ? (MigracionEsquemas.SeguimientoLevante, LevanteViejas)
            : (MigracionEsquemas.SeguimientoProduccion, ProduccionViejas);

        var prefijo = esquema.Columnas.Take(viejas.Length).Select(c => c.Titulo).ToArray();
        Assert.Equal(viejas, prefijo);
    }

    [Theory]
    [InlineData(nameof(MigracionEsquemas.SeguimientoLevante))]
    [InlineData(nameof(MigracionEsquemas.SeguimientoProduccion))]
    public void Postura_SoloFechaEsRequerida(string cual)
    {
        // Todo lo agregado (unidad, alimentos del inventario, categorías de huevo) es opcional.
        var esquema = cual == nameof(MigracionEsquemas.SeguimientoLevante)
            ? MigracionEsquemas.SeguimientoLevante
            : MigracionEsquemas.SeguimientoProduccion;

        var requeridas = esquema.Columnas.Where(c => c.Requerida).Select(c => c.Titulo).ToList();
        Assert.Equal(new[] { "Fecha" }, requeridas);
    }

    [Theory]
    [InlineData(nameof(MigracionEsquemas.SeguimientoLevante))]
    [InlineData(nameof(MigracionEsquemas.SeguimientoProduccion))]
    public void Postura_TieneLosOchoSlotsDeAlimentoDelInventario(string cual)
    {
        var esquema = cual == nameof(MigracionEsquemas.SeguimientoLevante)
            ? MigracionEsquemas.SeguimientoLevante
            : MigracionEsquemas.SeguimientoProduccion;
        var titulos = esquema.Columnas.Select(c => c.Titulo).ToList();

        foreach (var sexo in new[] { "H", "M" })
            foreach (var n in new[] { 1, 2 })
            {
                Assert.Contains($"Alimento {n} {sexo}", titulos);
                Assert.Contains($"Consumo Alimento {n} {sexo}", titulos);
            }

        var unidad = esquema.Columnas.Single(c => c.Titulo == "Unidad Consumo");
        Assert.Equal(new[] { "kg", "qq" }, unidad.Opciones);
    }

    [Theory]
    [InlineData(nameof(MigracionEsquemas.SeguimientoLevante))]
    [InlineData(nameof(MigracionEsquemas.SeguimientoProduccion))]
    public void Postura_TieneLas11CategoriasDeHuevo(string cual)
    {
        var esquema = cual == nameof(MigracionEsquemas.SeguimientoLevante)
            ? MigracionEsquemas.SeguimientoLevante
            : MigracionEsquemas.SeguimientoProduccion;
        var titulos = esquema.Columnas.Select(c => c.Titulo).ToList();

        foreach (var categoria in new[]
        {
            "Huevo Limpio", "Huevo Tratado", "Huevo Sucio", "Huevo Deforme", "Huevo Blanco",
            "Huevo Doble Yema", "Huevo Piso", "Huevo Pequeño", "Huevo Roto", "Huevo Desecho", "Huevo Otro"
        })
            Assert.Contains(categoria, titulos);
    }

    [Fact]
    public void SeguimientoLevante_TieneLasColumnasDeHuevoParaSemana14()
    {
        // Cierra el pendiente P2: las 11 categorías + peso. Total e incubables NO son columnas: se
        // derivan del desglose (HuevosClasificacion), como en el modal.
        var titulos = MigracionEsquemas.SeguimientoLevante.Columnas.Select(c => c.Titulo).ToList();
        Assert.Contains("Peso Huevo (g)", titulos);
        Assert.DoesNotContain("Huevo Total", titulos);
        Assert.DoesNotContain("Huevo Incubable", titulos);
    }

    [Fact]
    public void MovimientosAvesLevante_HojaColumnasYRequeridas()
    {
        // La hoja de traslados unilaterales de levante: el ORDEN es contrato (los operarios pegan
        // bloques enteros) y solo Fecha + Tipo son requeridas — "Lote Contraparte" se exige POR FILA
        // cuando el tipo es Salida, no a nivel de encabezado.
        var esquema = MigracionEsquemas.MovimientosAvesLevante;
        Assert.Equal("Movimientos Aves", esquema.Hoja);
        Assert.Equal(
            new[] { "Fecha", "Tipo", "Hembras", "Machos", "Lote Contraparte", "Granja Contraparte", "Observaciones" },
            esquema.Columnas.Select(c => c.Titulo).ToArray());
        Assert.Equal(
            new[] { "Fecha", "Tipo" },
            esquema.Columnas.Where(c => c.Requerida).Select(c => c.Titulo).ToArray());

        var tipo = esquema.Columnas.Single(c => c.Titulo == "Tipo");
        Assert.Equal(new[] { "Salida", "Ingreso" }, tipo.Opciones);
    }

    [Fact]
    public void AlimentoPostura_EsElMismoEsquemaQueEngorde()
    {
        // Una sola definición de la hoja "Alimento": si engorde gana una columna, postura la recibe.
        Assert.Same(MigracionEsquemas.AlimentoEngorde, MigracionEsquemas.AlimentoPostura);
        Assert.Equal("Alimento", MigracionEsquemas.AlimentoPostura.Hoja);
    }

    [Fact]
    public void HuevosPostura_ExigeFechaItemYCantidad()
    {
        var esquema = MigracionEsquemas.HuevosPostura;
        Assert.Equal("Huevos", esquema.Hoja);
        Assert.Equal(new[] { "Fecha", "Ítem", "Cantidad" },
            esquema.Columnas.Where(c => c.Requerida).Select(c => c.Titulo).ToArray());
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

    /// <summary>
    /// REGRESIÓN del bug de los títulos mixtos: el esquema aceptaba el encabezado (no salía como
    /// desconocido) pero el PARSEO lo buscaba con una lista de claves hardcodeada que no lo incluía,
    /// así que la columna entraba en CERO sin error ni advertencia. Con las claves derivadas del
    /// esquema, todo alias declarado es además una clave de lectura válida.
    /// </summary>
    [Fact]
    public void ClavesDeColumna_IncluyenElTituloYTodosLosAlias()
    {
        var claves = MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.SeguimientoPolloEngorde, "Consumo H (kg)");

        Assert.Contains("consumo h (kg)", claves);   // el título
        Assert.Contains("consumo h", claves);        // alias histórico
        Assert.Contains("consumo mixto (kg)", claves); // alias mixto (el que se perdía al leer)
    }

    [Fact]
    public void CadaTituloDeLaPlantillaMixta_EsClaveDeLecturaDeSuColumnaEnElEsquemaDeParseo()
    {
        // Para cada columna de la plantilla mixta buscamos la columna del esquema de parseo que la
        // acepta y verificamos que su título mixto esté entre las claves con las que se LEE la celda.
        var esquema = MigracionEsquemas.SeguimientoPolloEngorde;

        foreach (var columnaMixta in MigracionEsquemas.SeguimientoPolloEngordeMixto.Columnas)
        {
            var claveMixta = MigracionCalculos.NormalizarClave(columnaMixta.Titulo);

            var columnaDestino = esquema.Columnas.FirstOrDefault(c =>
                MigracionEsquemaCalculos.ClavesDeColumna(esquema, c.Titulo).Contains(claveMixta));

            Assert.True(columnaDestino is not null,
                $"El título mixto '{columnaMixta.Titulo}' no es clave de lectura de ninguna columna: " +
                "el archivo validaría pero esa columna se importaría en CERO.");
        }
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
