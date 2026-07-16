using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Mapeo puro fila de fn_vacunacion_cronograma_lote → VacunacionCronogramaItemDto:
/// registro aplanado → anidado, franja obligatoria (misma excepción que VacunacionCalculos.CalcularFranja)
/// y defaults defensivos.
/// </summary>
public class VacunacionCronogramaMapperTests
{
    private static VacunacionCronogramaItemRow Row(bool conRegistro = false, bool conFranja = true) => new()
    {
        Id = 33,
        LineaProductiva = "Levante",
        LoteId = 501,
        LoteNombre = "L-501",
        GranjaId = 10,
        GranjaNombre = "Granja Alfa",
        NucleoId = "N1",
        GalponId = "G3",
        ItemInventarioId = 7,
        ItemInventarioNombre = "Newcastle",
        UnidadObjetivo = "Semana",
        ValorObjetivo = 4,
        FechaObjetivo = null,
        RangoDiasAntes = 0,
        RangoDiasDespues = 6,
        FechaInicioFranja = conFranja ? new DateTime(2026, 3, 23) : null,
        FechaFinFranja = conFranja ? new DateTime(2026, 3, 29) : null,
        Orden = 2,
        Activo = true,
        Notas = "refuerzo",
        RegistroId = conRegistro ? 91 : null,
        RegistroEstado = conRegistro ? "AplicadoTardio" : null,
        RegistroFechaAplicacion = conRegistro ? new DateTime(2026, 4, 2) : null,
        RegistroDiasDesviacion = conRegistro ? 4 : null,
        RegistroIncumplido = conRegistro ? false : null,
        RegistroMotivo = conRegistro ? "clima" : null,
        UsuarioRegistraId = conRegistro ? 123456 : null,
        UsuarioRegistraNombre = conRegistro ? "Ana Pérez" : null,
        AplicadoPorUserId = conRegistro ? 654321 : null,
        AplicadoPorUserNombre = conRegistro ? "Luis Gómez" : null,
        AplicadoPorNombreLibre = null,
    };

    [Fact]
    public void ToDto_SinRegistro_RegistroEsNull()
    {
        var dto = VacunacionCronogramaMapper.ToDto(Row(conRegistro: false));
        Assert.Null(dto.Registro);
    }

    [Fact]
    public void ToDto_MapeaCamposDelItemUnoAUno()
    {
        var dto = VacunacionCronogramaMapper.ToDto(Row());

        Assert.Equal(33, dto.Id);
        Assert.Equal("Levante", dto.LineaProductiva);
        Assert.Equal(501, dto.LoteId);
        Assert.Equal("L-501", dto.LoteNombre);
        Assert.Equal(10, dto.GranjaId);
        Assert.Equal("Granja Alfa", dto.GranjaNombre);
        Assert.Equal("N1", dto.NucleoId);
        Assert.Equal("G3", dto.GalponId);
        Assert.Equal(7, dto.ItemInventarioId);
        Assert.Equal("Newcastle", dto.ItemInventarioNombre);
        Assert.Equal("Semana", dto.UnidadObjetivo);
        Assert.Equal(4, dto.ValorObjetivo);
        Assert.Null(dto.FechaObjetivo);
        Assert.Equal(0, dto.RangoDiasAntes);
        Assert.Equal(6, dto.RangoDiasDespues);
        Assert.Equal(new DateTime(2026, 3, 23), dto.FechaInicioFranja);
        Assert.Equal(new DateTime(2026, 3, 29), dto.FechaFinFranja);
        Assert.Equal(2, dto.Orden);
        Assert.True(dto.Activo);
        Assert.Equal("refuerzo", dto.Notas);
    }

    [Fact]
    public void ToDto_ConRegistro_ArmaElAnidadoConNombres()
    {
        var dto = VacunacionCronogramaMapper.ToDto(Row(conRegistro: true));

        Assert.NotNull(dto.Registro);
        var r = dto.Registro!;
        Assert.Equal(91, r.Id);
        Assert.Equal("AplicadoTardio", r.Estado);
        Assert.Equal(new DateTime(2026, 4, 2), r.FechaAplicacion);
        Assert.Equal(4, r.DiasDesviacion);
        Assert.False(r.Incumplido);
        Assert.Equal("clima", r.MotivoDescripcion);
        Assert.Equal(123456, r.UsuarioRegistraId);
        Assert.Equal("Ana Pérez", r.UsuarioRegistraNombre);
        Assert.Equal(654321, r.AplicadoPorUserId);
        Assert.Equal("Luis Gómez", r.AplicadoPorUserNombre);
        Assert.Null(r.AplicadoPorNombreLibre);
    }

    [Fact]
    public void ToDto_FranjaNull_LanzaMismaExcepcionQueElCalculoPuro()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => VacunacionCronogramaMapper.ToDto(Row(conFranja: false)));
        Assert.Contains("No se puede calcular la franja", ex.Message);
        Assert.Contains("Semana", ex.Message);
    }

    [Fact]
    public void ToDto_RegistroSinEstado_DefaultsDefensivos()
    {
        var row = Row(conRegistro: true);
        row.RegistroEstado = null;
        row.RegistroIncumplido = null;
        row.UsuarioRegistraId = null;

        var dto = VacunacionCronogramaMapper.ToDto(row);

        Assert.Equal("Pendiente", dto.Registro!.Estado);
        Assert.False(dto.Registro.Incumplido);
        Assert.Equal(0, dto.Registro.UsuarioRegistraId);
    }
}
