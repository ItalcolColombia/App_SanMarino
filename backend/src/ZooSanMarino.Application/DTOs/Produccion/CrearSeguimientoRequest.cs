// src/ZooSanMarino.Application/DTOs/Produccion/CrearSeguimientoRequest.cs
using System.ComponentModel.DataAnnotations;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.DTOs.Produccion;

public record CrearSeguimientoRequest(
    [Required] int ProduccionLoteId,
    [Required] DateTime FechaRegistro,
    [Required] [Range(0, int.MaxValue)] int MortalidadH,
    [Required] [Range(0, int.MaxValue)] int MortalidadM,
    [Required] [Range(0, int.MaxValue)] int SelH, // Selección hembras (retiradas)
    // Consumo con unidad opcional (el backend hace la conversión a kg)
    double? ConsumoH,
    string? UnidadConsumoH,
    double? ConsumoM,
    string? UnidadConsumoM,
    int? TipoAlimentoHembras,
    int? TipoAlimentoMachos,
    string? TipoItemHembras,
    string? TipoItemMachos,
    [Required] [Range(0, int.MaxValue)] int HuevosTotales,
    [Required] [Range(0, int.MaxValue)] int HuevosIncubables,
    [Range(0, int.MaxValue)] int HuevoLimpio,
    [Range(0, int.MaxValue)] int HuevoTratado,
    [Range(0, int.MaxValue)] int HuevoSucio,
    [Range(0, int.MaxValue)] int HuevoDeforme,
    [Range(0, int.MaxValue)] int HuevoBlanco,
    [Range(0, int.MaxValue)] int HuevoDobleYema,
    [Range(0, int.MaxValue)] int HuevoPiso,
    [Range(0, int.MaxValue)] int HuevoPequeno,
    [Range(0, int.MaxValue)] int HuevoRoto,
    [Range(0, int.MaxValue)] int HuevoDesecho,
    [Range(0, int.MaxValue)] int HuevoOtro,
    [Required] string TipoAlimento,
    [Required] [Range(0, double.MaxValue)] decimal PesoHuevo,
    [Required] [Range(1, 3)] int Etapa,
    string? Observaciones,
    [Range(0, double.MaxValue)] decimal? PesoH,
    [Range(0, double.MaxValue)] decimal? PesoM,
    [Range(0, 100)] decimal? Uniformidad,
    [Range(0, 100)] decimal? CoeficienteVariacion,
    string? ObservacionesPesaje,
    [Range(0, double.MaxValue)] double? ConsumoAguaDiario,
    [Range(0, 14)] double? ConsumoAguaPh,
    [Range(0, double.MaxValue)] double? ConsumoAguaOrp,
    [Range(0, double.MaxValue)] double? ConsumoAguaTemperatura,
    // Opcionales con valor por defecto (deben ir al final en C#)
    [Range(0, int.MaxValue)] int? ErrorSexajeHembras = 0,
    [Range(0, int.MaxValue)] int? ErrorSexajeMachos = 0,
    [Range(0, 100)] double? UniformidadHembras = null,
    [Range(0, 100)] double? UniformidadMachos = null,
    [Range(0, 100)] double? CvHembras = null,
    [Range(0, 100)] double? CvMachos = null,
    string? Ciclo = null,
    [Range(0, int.MaxValue)] int SelM = 0,
    List<ItemSeguimientoDto>? ItemsHembras = null,
    List<ItemSeguimientoDto>? ItemsMachos = null,
    string? CreatedByUserId = null
);
