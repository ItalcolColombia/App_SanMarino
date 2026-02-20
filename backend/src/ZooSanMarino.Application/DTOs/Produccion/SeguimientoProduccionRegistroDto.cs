// DTO con los campos de un registro de seguimiento diario producción.
// Usado por liquidación e indicadores; permite leer desde SeguimientoProduccion o seguimiento_diario (tipo produccion).
namespace ZooSanMarino.Application.DTOs.Produccion;

public record SeguimientoProduccionRegistroDto(
    DateTime Fecha,
    int MortalidadH,
    int MortalidadM,
    int SelH,
    int SelM,
    decimal ConsKgH,
    decimal ConsKgM,
    int HuevoTot,
    int HuevoInc,
    int HuevoLimpio,
    int HuevoTratado,
    int HuevoSucio,
    int HuevoDeforme,
    int HuevoBlanco,
    int HuevoDobleYema,
    int HuevoPiso,
    int HuevoPequeno,
    int HuevoRoto,
    int HuevoDesecho,
    int HuevoOtro,
    decimal PesoHuevo,
    int Etapa,
    decimal? PesoH,
    decimal? PesoM,
    decimal? Uniformidad,
    decimal? CoeficienteVariacion,
    string? ObservacionesPesaje
);
