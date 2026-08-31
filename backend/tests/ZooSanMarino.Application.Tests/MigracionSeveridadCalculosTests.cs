using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fija qué descarta una fila de una importación masiva y qué es solo un aviso.
/// El caso que motivó todo: una Advertencia tiraba el día completo y el resumen decía «Procesado».
/// </summary>
public class MigracionSeveridadCalculosTests
{
    private static MigracionErrorDto Err(int fila, string msg = "roto") => new(fila, "Col", null, msg);
    private static MigracionErrorDto Adv(int fila, string msg = "aviso") =>
        new(fila, "Col", null, msg, MigracionSeveridadCalculos.Advertencia);

    [Fact]
    public void UnErrorDescartaLaFila()
    {
        Assert.True(MigracionSeveridadCalculos.DescartaLaFila(Err(3)));
    }

    [Fact]
    public void UnaAdvertenciaNoDescartaLaFila()
    {
        // El caso real: «se ignora el consumo directo: la fila trae alimentos del inventario».
        Assert.False(MigracionSeveridadCalculos.DescartaLaFila(
            Adv(3, "Se ignora el consumo directo de 'Consumo H (kg)'.")));
    }

    [Fact]
    public void LaSeveridadPorDefectoEsError()
    {
        // MigracionErrorDto nace con Severidad = "Error": los lectores de celdas cuentan con eso.
        var e = new MigracionErrorDto(1, "Fecha", null, "Fecha inválida o faltante.");
        Assert.Equal(MigracionSeveridadCalculos.Error, e.Severidad);
        Assert.True(MigracionSeveridadCalculos.DescartaLaFila(e));
    }

    [Fact]
    public void SoloLasAdvertencias_NoMuevenElContador()
    {
        // Es el corazón del defecto: antes esto daba 3 y la fila se perdía entera.
        var errores = new List<MigracionErrorDto> { Adv(5), Adv(5), Adv(5) };

        Assert.Equal(0, MigracionSeveridadCalculos.CuentaQueDescartan(errores));
    }

    [Fact]
    public void ElContadorSubeSoloConErroresReales()
    {
        var errores = new List<MigracionErrorDto>();
        var marca = MigracionSeveridadCalculos.CuentaQueDescartan(errores);

        errores.Add(Adv(7));
        Assert.False(MigracionSeveridadCalculos.CuentaQueDescartan(errores) > marca);

        errores.Add(Err(7));
        Assert.True(MigracionSeveridadCalculos.CuentaQueDescartan(errores) > marca);
    }

    [Fact]
    public void FilasConError_CuentaFilasDistintas_NoErrores()
    {
        var errores = new List<MigracionErrorDto> { Err(4), Err(4), Err(9), Adv(11) };

        Assert.Equal(2, MigracionSeveridadCalculos.FilasConError(errores));
    }

    [Fact]
    public void FilasConError_IgnoraLosDelArchivoEntero()
    {
        // El error de stock nace con Fila = 0: no es de una fila, es del archivo.
        var errores = new List<MigracionErrorDto>
        {
            new(0, "Alimento", "POLLA LEVANTE", "No alcanza el stock: faltan 382.310 kg."),
        };

        Assert.Equal(0, MigracionSeveridadCalculos.FilasConError(errores));
        Assert.True(MigracionSeveridadCalculos.HayErroresDelArchivo(errores));
    }

    [Fact]
    public void HayErroresDelArchivo_EsFalsoSiTodoEsPorFila()
    {
        var errores = new List<MigracionErrorDto> { Err(2), Err(3), Adv(0) };

        Assert.False(MigracionSeveridadCalculos.HayErroresDelArchivo(errores));
    }

    [Fact]
    public void ElCasoS369_FilasConErrorCeroPeroArchivoRechazado()
    {
        // Reproduce el estado que dejó encerrado al usuario: filas_error = 0 en la base, el archivo
        // rechazado entero, y el front deduciendo de ese 0 que no hay nada que reintentar.
        var errores = new List<MigracionErrorDto>
        {
            new(0, "Alimento", "POLLA LEVANTE REPRODUCTORA PESADA",
                "No alcanza el stock en la granja: el archivo consume 846.500 kg y solo hay 464.190 kg."),
        };

        Assert.Equal(0, MigracionSeveridadCalculos.FilasConError(errores));
        Assert.True(MigracionSeveridadCalculos.HayErroresDelArchivo(errores));
    }
}
