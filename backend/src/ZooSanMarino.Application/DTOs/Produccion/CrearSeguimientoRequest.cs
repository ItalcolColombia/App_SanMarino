// src/ZooSanMarino.Application/DTOs/Produccion/CrearSeguimientoRequest.cs
using System.ComponentModel.DataAnnotations;

namespace ZooSanMarino.Application.DTOs.Produccion;

public record CrearSeguimientoRequest(
    [Required] int ProduccionLoteId,
    [Required] DateTime FechaRegistro,
    [Required] [Range(0, int.MaxValue)] int MortalidadH,
    [Required] [Range(0, int.MaxValue)] int MortalidadM,
    [Required] [Range(0, int.MaxValue)] int SelH, // Selección hembras (retiradas)
    [Required] [Range(0, double.MaxValue)] decimal ConsKgH, // Consumo hembras (kg)
    [Required] [Range(0, double.MaxValue)] decimal ConsKgM, // Consumo machos (kg)
    [Required] [Range(0, int.MaxValue)] int HuevosTotales,
    [Required] [Range(0, int.MaxValue)] int HuevosIncubables,
    // Campos de Clasificadora de Huevos - (Limpio, Tratado) = HuevoInc +
    [Range(0, int.MaxValue)] int HuevoLimpio,
    [Range(0, int.MaxValue)] int HuevoTratado,
    // Campos de Clasificadora de Huevos - (Sucio, Deforme, Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, Otro) = Huevo Total
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
    [Required] [Range(1, 3)] int Etapa, // 1: semana 25-33, 2: 34-50, 3: >50
    string? Observaciones,
    // Campos de Pesaje Semanal (registro una vez por semana)
    [Range(0, double.MaxValue)] decimal? PesoH, // Peso promedio hembras (kg)
    [Range(0, double.MaxValue)] decimal? PesoM, // Peso promedio machos (kg)
    [Range(0, 100)] decimal? Uniformidad, // Uniformidad del lote (%)
    [Range(0, 100)] decimal? CoeficienteVariacion, // Coeficiente de variación (CV)
    string? ObservacionesPesaje // Observaciones específicas del pesaje
);



