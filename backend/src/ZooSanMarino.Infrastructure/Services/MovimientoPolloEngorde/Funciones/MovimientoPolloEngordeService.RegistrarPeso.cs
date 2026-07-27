// MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.RegistrarPeso.cs
// Carga del peso báscula de un despacho COMPLETO cuando la báscula llega después de la venta
// (empresas con companies.venta_engorde_peso_diferido — Panamá).
//
// Por qué el peso entra ANTES de la transición de estado:
//   La venta nace 'Pendiente' = "registrada, esperando báscula". Al cargar el peso se re-prorratea
//   y, en la MISMA transacción, se completan las líneas pendientes. Así nunca existe una venta
//   'Completado' sin peso ⇒ el detector MOV_SIN_PESO de fn_auditoria_liquidacion_engorde (que
//   filtra estado='Completado') y los reportes de liquidación quedan intactos, y no hay que
//   relajar ninguno de los dos gates de estado (edición y completar).
//
// Por qué el peso se carga por FACTURA y nunca por movimiento suelto:
//   PesoBruto/PesoTara son el peso GLOBAL del camión, clonado en cada línea del despacho; el
//   individual correcto es el prorrateo por aves. Cargarlo línea a línea reproduce exactamente el
//   daño histórico que OrganizarPeso vino a reparar (netos sobrecontados n×).
//
// Se escriben los 9 campos de peso con la MISMA aritmética de la creación
// (MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea: 3 decimales y residuo a la línea con más
// aves). Escribir PesoNeto/PesoTaraReal/PromedioPesoAve es OBLIGATORIO además por otra razón: el
// trigger del espejo histórico (trg_lote_hist_mov_pollo) sólo escucha
// `UPDATE OF cantidad_*, peso_neto, peso_tara_real, promedio_peso_ave, ...` — peso_bruto y
// peso_tara NO están en esa lista, así que un UPDATE que sólo tocara la báscula dejaría
// lote_registro_historico_unificado en 0 kg para siempre.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoPolloEngordeService
{
    /// <inheritdoc />
    public async Task<RegistrarPesoFacturaResponse> RegistrarPesoFacturaAsync(
        Guid facturaId, RegistrarPesoFacturaRequest request)
    {
        if (facturaId == Guid.Empty)
            throw new InvalidOperationException("Debe indicar el despacho (factura) al que pertenece el peso.");
        if (request is null)
            throw new InvalidOperationException("Debe enviar el peso báscula del despacho.");

        // Mismas reglas y mensajes que al registrar la venta con peso: al cargar la báscula el peso
        // ya no es opcional (flag apagado a propósito).
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta(
            "Venta", request.PesoBruto, request.PesoTara);

        var pesoBruto = request.PesoBruto!.Value;
        var pesoTara = request.PesoTara!.Value;

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var lineas = await _ctx.MovimientoPolloEngorde
                .Where(x => x.FacturaId == facturaId
                            && x.CompanyId == _currentUser.CompanyId
                            && x.DeletedAt == null
                            && x.Estado != "Cancelado")
                .OrderBy(x => x.Id)
                .ToListAsync();

            if (lineas.Count == 0)
                throw new InvalidOperationException("El despacho no existe, fue eliminado o no pertenece a la empresa activa.");
            if (lineas.Any(l => l.Estado == "Anulado"))
                throw new InvalidOperationException("El despacho tiene líneas anuladas: no se puede registrar el peso.");

            var pesoNetoGlobal = AplicarPesoDespacho(lineas, pesoBruto, pesoTara);

            var completados = 0;
            if (request.Confirmar)
            {
                // CompleteAsync guarda dentro; los cambios de peso de arriba viajan en el mismo
                // SaveChanges porque comparten el DbContext y la transacción.
                foreach (var id in lineas.Where(l => l.Estado == "Pendiente").Select(l => l.Id).ToList())
                {
                    var dto = await CompleteAsync(id);
                    if (dto == null)
                        throw new InvalidOperationException($"Movimiento {id} no encontrado al completar el despacho.");
                    completados++;
                }
            }

            // Si no se completó nada, el peso todavía no se persistió.
            if (completados == 0) await _ctx.SaveChangesAsync();
            await tx.CommitAsync();

            var totalAves = lineas.Sum(l => l.TotalAves);
            var response = new RegistrarPesoFacturaResponse
            {
                FacturaId = facturaId,
                MovimientosActualizados = lineas.Count,
                MovimientosCompletados = completados,
                TotalAves = totalAves,
                PesoBrutoGlobal = pesoBruto,
                PesoTaraGlobal = pesoTara,
                PesoNetoGlobal = pesoNetoGlobal,
                PromedioPesoAve = totalAves > 0 ? pesoNetoGlobal / totalAves : null,
                Mensaje = completados > 0
                    ? $"Peso registrado y despacho confirmado: {completados} movimiento(s) completado(s), {pesoNetoGlobal:0.###} kg netos. Se descontaron las aves en cada lote."
                    : $"Peso del despacho actualizado en {lineas.Count} movimiento(s): {pesoNetoGlobal:0.###} kg netos. No se modificaron estados ni saldos de aves."
            };

            foreach (var l in lineas)
            {
                var d = await GetByIdAsync(l.Id);
                if (d != null) response.Movimientos.Add(d);
            }
            return response;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Escribe el peso global del camión y su prorrateo por aves en TODAS las líneas del despacho.
    /// Espejo exacto de lo que hace la creación de la venta (Panamá y venta por granja): mismos 9
    /// campos, mismo <see cref="MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea"/>.
    /// Devuelve el neto global.
    /// </summary>
    private double AplicarPesoDespacho(List<MovimientoPolloEngorde> lineas, double pesoBruto, double pesoTara)
    {
        var pesoNetoGlobal = pesoBruto - pesoTara;
        var prorrateo = MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(
            pesoBruto, pesoTara, lineas.Select(l => l.TotalAves).ToList());

        for (int i = 0; i < lineas.Count; i++)
        {
            var l = lineas[i];
            l.PesoBruto = pesoBruto;
            l.PesoTara = pesoTara;
            l.PesoBrutoGlobal = pesoBruto;
            l.PesoTaraGlobal = pesoTara;
            l.PesoNetoGlobal = pesoNetoGlobal;
            l.PesoBrutoReal = prorrateo[i].Bruto;
            l.PesoTaraReal = prorrateo[i].Tara;
            l.PesoNeto = prorrateo[i].Neto;
            l.PromedioPesoAve = prorrateo[i].Promedio;
            l.UpdatedByUserId = _currentUser.UserId;
            l.UpdatedAt = DateTime.UtcNow;
        }
        return pesoNetoGlobal;
    }
}
