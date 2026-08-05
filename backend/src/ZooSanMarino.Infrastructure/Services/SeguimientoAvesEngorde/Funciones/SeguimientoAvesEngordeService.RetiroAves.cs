// src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngorde/Funciones/SeguimientoAvesEngordeService.RetiroAves.cs
// Envoltorio delgado sobre RetiroAvesEngordeAplicador: el descuento de aves por las bajas del
// seguimiento es idéntico en los dos caminos de captura (formulario diario y carga masiva), así que
// la lógica vive una sola vez en el aplicador compartido.
namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeService
{
    /// <inheritdoc cref="RetiroAvesEngordeAplicador.SincronizarAsync"/>
    private Task SincronizarBajasAvesAsync(
        int loteAveEngordeId, long seguimientoId, DateTime fecha,
        int bajasHembrasNuevas, int bajasMachosNuevas) =>
        RetiroAvesEngordeAplicador.SincronizarAsync(
            _ctx, _current.CompanyId, loteAveEngordeId, seguimientoId, fecha,
            bajasHembrasNuevas, bajasMachosNuevas);
}
