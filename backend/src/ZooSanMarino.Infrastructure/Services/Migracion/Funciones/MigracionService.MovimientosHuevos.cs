// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.MovimientosHuevos.cs
// Hoja "Movimientos Huevos" de la carga masiva de Seguimiento PRODUCCIÓN: salidas de huevos del
// lote hacia PLANTA (Traslado) o por VENTA.
//
// Aplica el patrón de la spec de Fase 3: INSERT directo en `traslado_huevos` en estado Completado
// (número HUE-… en un segundo SaveChanges, igual que el servicio vivo) + UN recálculo ABSOLUTO del
// espejo al final (RecalcularEspejoHuevoProduccionAsync es idempotente: histórico = Σ producción,
// dinámico = histórico − Σ movimientos Completado). NUNCA se llama a
// TrasladoHuevosService.CrearTrasladoHuevosAsync (auto-procesa fila a fila, valida contra un espejo
// que estaría desactualizado a mitad de la carga y TRAGA excepciones dejando Pendientes mudos) y
// NUNCA se escriben filas de descuento en seguimiento_diario_levante (ahí vive el trigger del
// espejo: el descuento se aplicaría dos veces).
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    /// <summary>Una fila ya validada de la hoja "Movimientos Huevos".</summary>
    private sealed record MovimientoHuevosMigFila(
        int NumeroFila,
        DateTime Fecha,
        MovimientoHuevosMigracion Tipo,
        HuevosClasificacion Cantidades,
        string TipoDestino,
        string? Destino,
        string? Motivo,
        string? Descripcion,
        string? Observaciones);

    /// <summary>Claves de lectura (título + alias) de una columna de la hoja "Movimientos Huevos".</summary>
    private static string[] ClavesMovHuevos(string titulo) =>
        MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.MovimientosHuevosProduccion, titulo);

    /// <summary>
    /// Lee y valida la hoja "Movimientos Huevos" (opcional; solo PRODUCCIÓN la invoca — levante la
    /// ignora igual que ignora la hoja "Huevos").
    /// </summary>
    private List<MovimientoHuevosMigFila> LeerHojaMovimientosHuevos(
        IFormFile file, LotePosturaCtx? loteCtx, List<MigracionErrorDto> errores)
    {
        var filas = LeerHojaOpcionalConEsquema(file, MigracionEsquemas.MovimientosHuevosProduccion, errores);
        if (filas.Count == 0) return new List<MovimientoHuevosMigFila>();

        var resultado = new List<MovimientoHuevosMigFila>();
        var clavesArchivo = new HashSet<string>();
        var hoyUtc = DateTime.UtcNow.Date;

        foreach (var fila in filas)
        {
            int e0 = errores.Count;

            if (!MigracionCalculos.TryFecha(Celda(fila, ClavesMovHuevos("Fecha")), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Movimientos Huevos: fecha inválida o faltante.")); continue; }
            if (!ValidarFechaContraLote(fila, fecha, loteCtx, hoyUtc, errores)) continue;

            var tipoTexto = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevos("Tipo")));
            if (!MigracionMovimientosHuevosCalculos.TryOperacion(tipoTexto, out var tipo))
            {
                errores.Add(new(fila.Numero, "Tipo", tipoTexto,
                    "Movimientos Huevos: tipo no reconocido. Usá 'Traslado' (a planta) o 'Venta'."));
                continue;
            }

            int Cat(string titulo) => EnteroNoNeg(fila, errores, titulo, ClavesMovHuevos(titulo));
            var cantidades = new HuevosClasificacion(
                Limpio: Cat("Huevo Limpio"), Tratado: Cat("Huevo Tratado"), Sucio: Cat("Huevo Sucio"),
                Deforme: Cat("Huevo Deforme"), Blanco: Cat("Huevo Blanco"), DobleYema: Cat("Huevo Doble Yema"),
                Piso: Cat("Huevo Piso"), Pequeno: Cat("Huevo Pequeño"), Roto: Cat("Huevo Roto"),
                Desecho: Cat("Huevo Desecho"), Otro: Cat("Huevo Otro"));
            if (errores.Count > e0) continue;
            if (cantidades.Totales <= 0)
            {
                errores.Add(new(fila.Numero, "Huevo Limpio", null,
                    "Movimientos Huevos: indicá cuántos huevos se mueven en al menos una categoría (mayor a 0)."));
                continue;
            }

            var tipoDestinoTexto = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevos("Tipo Destino")));
            var tipoDestino = MigracionMovimientosHuevosCalculos.TipoDestinoEfectivo(tipoDestinoTexto, tipo);
            if (tipoDestino is null)
            {
                errores.Add(new(fila.Numero, "Tipo Destino", tipoDestinoTexto,
                    $"Movimientos Huevos: tipo de destino no reconocido. Usá {string.Join(", ", MigracionMovimientosHuevosCalculos.TiposDestino)} (vacío = default según el tipo)."));
                continue;
            }

            var clave = MigracionMovimientosHuevosCalculos.ClaveArchivo(fecha.Date, tipo, cantidades);
            if (!clavesArchivo.Add(clave))
            {
                errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"),
                    "Movimientos Huevos: fila repetida (misma fecha, tipo y cantidades). Si son dos movimientos reales el mismo día, cargalos como una sola fila sumada."));
                continue;
            }

            resultado.Add(new MovimientoHuevosMigFila(
                fila.Numero, fecha.Date, tipo, cantidades, tipoDestino,
                MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevos("Destino"))),
                MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevos("Motivo"))),
                MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevos("Descripción"))),
                MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevos("Observaciones")))));
        }

        return resultado;
    }

    /// <summary>
    /// Disponibilidad proyectada por categoría: espejo dinámico actual + huevos que el archivo va a
    /// producir − huevos que el archivo va a mover. Si alguna categoría queda en negativo es un
    /// ERROR (mismo criterio del módulo vivo, que rechaza el traslado): el clamp del espejo lo
    /// taparía en silencio. Aproximación documentada: en un reimport los huevos de días ya cargados
    /// también están en el espejo, así que la proyección puede sobrar — nunca faltar.
    /// </summary>
    private async Task ValidarDisponibilidadHuevosProyectadaAsync(
        int loteId,
        IReadOnlyList<Dictionary<string, object?>> filasJson,
        IReadOnlyList<MovimientoHuevosMigFila> movimientosHuevos,
        List<MigracionErrorDto> errores,
        CancellationToken ct)
    {
        if (movimientosHuevos.Count == 0) return;

        var espejo = await _ctx.LotePosturaProduccion.AsNoTracking()
            .Where(p => p.LoteId == loteId && p.DeletedAt == null)
            .Join(_ctx.EspejoHuevoProduccion.AsNoTracking(),
                p => p.LotePosturaProduccionId, e => e.LotePosturaProduccionId,
                (p, e) => e)
            .FirstOrDefaultAsync(ct);

        static int Prod(IReadOnlyList<Dictionary<string, object?>> filas, string clave) =>
            filas.Sum(f => f.TryGetValue(clave, out var v) && v is int n ? n : 0);

        var categorias = new (string Nombre, string ClaveJson, int Dinamico, Func<HuevosClasificacion, int> Mov)[]
        {
            ("Huevo Limpio", "huevo_limpio", espejo?.HuevoLimpioDinamico ?? 0, c => c.Limpio),
            ("Huevo Tratado", "huevo_tratado", espejo?.HuevoTratadoDinamico ?? 0, c => c.Tratado),
            ("Huevo Sucio", "huevo_sucio", espejo?.HuevoSucioDinamico ?? 0, c => c.Sucio),
            ("Huevo Deforme", "huevo_deforme", espejo?.HuevoDeformeDinamico ?? 0, c => c.Deforme),
            ("Huevo Blanco", "huevo_blanco", espejo?.HuevoBlancoDinamico ?? 0, c => c.Blanco),
            ("Huevo Doble Yema", "huevo_doble_yema", espejo?.HuevoDobleYemaDinamico ?? 0, c => c.DobleYema),
            ("Huevo Piso", "huevo_piso", espejo?.HuevoPisoDinamico ?? 0, c => c.Piso),
            ("Huevo Pequeño", "huevo_pequeno", espejo?.HuevoPequenoDinamico ?? 0, c => c.Pequeno),
            ("Huevo Roto", "huevo_roto", espejo?.HuevoRotoDinamico ?? 0, c => c.Roto),
            ("Huevo Desecho", "huevo_desecho", espejo?.HuevoDesechoDinamico ?? 0, c => c.Desecho),
            ("Huevo Otro", "huevo_otro", espejo?.HuevoOtroDinamico ?? 0, c => c.Otro),
        };

        foreach (var (nombre, claveJson, dinamico, mov) in categorias)
        {
            var producidos = Prod(filasJson, claveJson);
            var movidos = movimientosHuevos.Sum(m => mov(m.Cantidades));
            var proyectado = dinamico + producidos - movidos;
            if (proyectado < 0)
                errores.Add(new(0, "Movimientos Huevos", nombre,
                    $"No alcanzan los huevos de '{nombre}': disponibles {dinamico} + {producidos} del archivo − {movidos} a mover = {proyectado}. Corregí las cantidades o cargá primero la producción."));
        }
    }

    /// <summary>
    /// Aplica los movimientos DESPUÉS de la fn (la producción del archivo ya existe). Idempotente
    /// contra <c>traslado_huevos</c>: un movimiento Completado del mismo día con la misma operación
    /// y las mismas 11 cantidades se OMITE. Cada fila commitea por separado (dos SaveChanges: el
    /// número HUE-… necesita el id, igual que el servicio vivo); un fallo se reporta y no tumba el
    /// resto. El espejo se recalcula UNA vez al final (el llamador).
    /// </summary>
    private async Task<(int Aplicados, int Omitidos)> AplicarMovimientosHuevosAsync(
        IReadOnlyList<MovimientoHuevosMigFila> movimientos, int loteId,
        List<MigracionErrorDto> fallos, CancellationToken ct)
    {
        if (movimientos.Count == 0) return (0, 0);

        var lpp = await _ctx.LotePosturaProduccion.AsNoTracking()
            .Where(x => x.LoteId == loteId && x.DeletedAt == null && x.LotePosturaProduccionId != null)
            .OrderByDescending(x => x.LotePosturaProduccionId)
            .Select(x => new { Id = x.LotePosturaProduccionId!.Value, LoteId = x.LoteId!.Value, x.GranjaId, x.CompanyId })
            .FirstOrDefaultAsync(ct);
        if (lpp is null)
        {
            fallos.Add(new(0, "Movimientos Huevos", null,
                "No se encontró el lote de producción: los movimientos de huevos no se aplicaron."));
            return (0, 0);
        }

        var desde = movimientos.Min(m => m.Fecha).Date.AddDays(-1);
        var hasta = movimientos.Max(m => m.Fecha).Date.AddDays(2);
        var clavesExistentes = (await _ctx.TrasladoHuevos.AsNoTracking()
                .Where(t => t.LotePosturaProduccionId == lpp.Id && t.DeletedAt == null
                            && t.Estado != "Cancelado"
                            && t.FechaTraslado >= desde && t.FechaTraslado < hasta)
                .Select(t => new
                {
                    t.FechaTraslado, t.TipoOperacion,
                    t.CantidadLimpio, t.CantidadTratado, t.CantidadSucio, t.CantidadDeforme, t.CantidadBlanco,
                    t.CantidadDobleYema, t.CantidadPiso, t.CantidadPequeno, t.CantidadRoto, t.CantidadDesecho, t.CantidadOtro
                })
                .ToListAsync(ct))
            .Where(t => MigracionMovimientosHuevosCalculos.TryOperacion(t.TipoOperacion, out _))
            .Select(t =>
            {
                MigracionMovimientosHuevosCalculos.TryOperacion(t.TipoOperacion, out var op);
                return MigracionMovimientosHuevosCalculos.ClaveArchivo(t.FechaTraslado.Date, op, new HuevosClasificacion(
                    Limpio: t.CantidadLimpio, Tratado: t.CantidadTratado, Sucio: t.CantidadSucio,
                    Deforme: t.CantidadDeforme, Blanco: t.CantidadBlanco, DobleYema: t.CantidadDobleYema,
                    Piso: t.CantidadPiso, Pequeno: t.CantidadPequeno, Roto: t.CantidadRoto,
                    Desecho: t.CantidadDesecho, Otro: t.CantidadOtro));
            })
            .ToHashSet();

        int aplicados = 0, omitidos = 0;
        foreach (var m in movimientos.OrderBy(x => x.Fecha).ThenBy(x => x.NumeroFila))
        {
            if (clavesExistentes.Contains(MigracionMovimientosHuevosCalculos.ClaveArchivo(m.Fecha, m.Tipo, m.Cantidades)))
            { omitidos++; continue; }

            try
            {
                var fechaUtc = DateTime.UtcNow;
                var traslado = new TrasladoHuevos
                {
                    FechaTraslado = m.Fecha.Date.AddHours(12), // mediodía: ver gotcha de fechas puras
                    TipoOperacion = m.Tipo == MovimientoHuevosMigracion.Venta ? "Venta" : "Traslado",
                    LoteId = lpp.LoteId.ToString(),
                    LotePosturaProduccionId = lpp.Id,
                    GranjaOrigenId = lpp.GranjaId,
                    LoteDestinoId = m.Destino,
                    TipoDestino = m.TipoDestino,
                    Motivo = m.Motivo,
                    Descripcion = m.Descripcion,
                    Observaciones = string.IsNullOrWhiteSpace(m.Observaciones)
                        ? "Carga masiva de seguimiento producción"
                        : $"Carga masiva de seguimiento producción. {m.Observaciones}",
                    CantidadLimpio = m.Cantidades.Limpio,
                    CantidadTratado = m.Cantidades.Tratado,
                    CantidadSucio = m.Cantidades.Sucio,
                    CantidadDeforme = m.Cantidades.Deforme,
                    CantidadBlanco = m.Cantidades.Blanco,
                    CantidadDobleYema = m.Cantidades.DobleYema,
                    CantidadPiso = m.Cantidades.Piso,
                    CantidadPequeno = m.Cantidades.Pequeno,
                    CantidadRoto = m.Cantidades.Roto,
                    CantidadDesecho = m.Cantidades.Desecho,
                    CantidadOtro = m.Cantidades.Otro,
                    Estado = "Completado",
                    FechaProcesamiento = fechaUtc,
                    UsuarioTrasladoId = _current.UserId,
                    CompanyId = lpp.CompanyId,
                    CreatedByUserId = _current.UserId,
                    CreatedAt = fechaUtc
                };
                _ctx.TrasladoHuevos.Add(traslado);
                await _ctx.SaveChangesAsync(ct);

                traslado.NumeroTraslado = traslado.GenerarNumeroTraslado();
                await _ctx.SaveChangesAsync(ct);
                aplicados++;
            }
            catch (Exception ex)
            {
                fallos.Add(new(m.NumeroFila, "Movimientos Huevos", m.Fecha.ToString("yyyy-MM-dd"),
                    $"El movimiento de huevos del {m.Fecha:yyyy-MM-dd} no se pudo aplicar: {ex.Message}"));
            }
        }
        return (aplicados, omitidos);
    }

    /// <summary>
    /// Recalcula el espejo de huevos del lote de producción UNA vez. El alta manual lo hace tras
    /// cada registro; la fn de la carga masiva no lo hacía nunca — los huevos cargados por Excel
    /// dejaban la disponibilidad desactualizada. El recálculo es absoluto e idempotente. Un fallo
    /// no tumba la carga (el espejo se puede reconstruir).
    /// </summary>
    private async Task RecalcularEspejoHuevosAsync(int loteId, List<MigracionErrorDto> fallos, CancellationToken ct)
    {
        if (_espejoHuevoSync is null) return;
        var lppId = await _ctx.LotePosturaProduccion.AsNoTracking()
            .Where(x => x.LoteId == loteId && x.DeletedAt == null && x.LotePosturaProduccionId != null)
            .OrderByDescending(x => x.LotePosturaProduccionId)
            .Select(x => (int?)x.LotePosturaProduccionId!.Value)
            .FirstOrDefaultAsync(ct);
        if (lppId is null) return;

        try
        {
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lppId.Value, ct);
        }
        catch (Exception ex)
        {
            fallos.Add(new(0, "Movimientos Huevos", null,
                $"La carga se aplicó pero el espejo de huevos no se pudo recalcular: {ex.Message}", "Advertencia"));
        }
    }
}
