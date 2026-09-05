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
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
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
        string? Observaciones,
        IReadOnlyList<HuevoItemSeguimientoDto>? HuevoItems = null)
    {
        /// <summary>¿El movimiento viene clasificado por ítem del catálogo en vez de por las 11 categorías?</summary>
        public bool PorItems => HuevoItems is { Count: > 0 };

        /// <summary>Total de huevos del movimiento, venga por ítems o por categorías.</summary>
        public int Total => PorItems ? HuevoItemsCalculos.SumarTotal(HuevoItems) : Cantidades.Totales;
    }

    /// <summary>Claves de lectura (título + alias) de una columna de la hoja "Movimientos Huevos".</summary>
    private static string[] ClavesMovHuevos(string titulo) =>
        MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.MovimientosHuevosProduccion, titulo);

    /// <summary>Claves de lectura de la variante POR ÍTEM de la hoja "Movimientos Huevos".</summary>
    private static string[] ClavesMovHuevosItem(string titulo) =>
        MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.MovimientosHuevosPorItem, titulo);

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
    /// Lee la hoja "Movimientos Huevos" en su variante POR ÍTEM del catálogo (empresas con
    /// <c>clasificacion_huevo_por_items</c>). Una fila por ítem; las filas que comparten fecha, tipo
    /// y destino forman UN movimiento con N ítems — mismo criterio con el que la venta de engorde
    /// arma un despacho a partir de varias filas.
    ///
    /// <para>
    /// El ítem se resuelve contra los tipos de huevo DECLARADOS POR EL LOTE, exactamente como la hoja
    /// "Huevos": si no aplicara la misma lista blanca, la carga masiva sería la puerta de atrás de la
    /// restricción que pidió el cliente (F7.3).
    /// </para>
    /// </summary>
    private async Task<List<MovimientoHuevosMigFila>> LeerHojaMovimientosHuevosPorItemAsync(
        IFormFile file, int companyId, LotePosturaCtx? loteCtx, List<MigracionErrorDto> errores, CancellationToken ct)
    {
        var filas = LeerHojaOpcionalConEsquema(file, MigracionEsquemas.MovimientosHuevosPorItem, errores);
        if (filas.Count == 0 || loteCtx is null) return new List<MovimientoHuevosMigFila>();

        // 🔴 A diferencia de la hoja "Huevos" (producción), acá NO se aplica la lista blanca del lote
        // (F7.3): un traslado mueve lo que YA se produjo, y un tipo que salió de la lista del lote
        // sigue teniendo huevos que sacar. Es la misma decisión, documentada, que toma el alta manual
        // en `TrasladoHuevosService.ValidarCatalogoHuevoItemsAsync`. Lo que acota de verdad es la
        // DISPONIBILIDAD por ítem, más abajo.
        var catalogo = await CargarItemsHuevoEmpresaAsync(companyId, ct);
        if (catalogo.Count == 0)
        {
            errores.Add(new(0, "Ítem", null,
                "La empresa no tiene tipos de huevo activos en su catálogo: no se pueden cargar movimientos de huevo."));
            return new List<MovimientoHuevosMigFila>();
        }

        var porClave = new Dictionary<string, List<(int Id, string? Codigo, string Nombre, string? TipoHuevo)>>();
        foreach (var i in catalogo)
        {
            Indexar(i.Nombre, i);
            Indexar(i.Codigo, i);
        }

        // (fecha, tipo, destino...) -> movimiento en construcción. Se conserva el orden de aparición.
        var grupos = new Dictionary<string, (MovimientoHuevosMigFila Cabecera, List<HuevoItemSeguimientoDto> Items)>();
        var orden = new List<string>();
        var hoyUtc = DateTime.UtcNow.Date;

        foreach (var fila in filas)
        {
            int e0 = errores.Count;

            if (!MigracionCalculos.TryFecha(Celda(fila, ClavesMovHuevosItem("Fecha")), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Movimientos Huevos: fecha inválida o faltante.")); continue; }
            if (!ValidarFechaContraLote(fila, fecha, loteCtx, hoyUtc, errores)) continue;

            var tipoTexto = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevosItem("Tipo")));
            if (!MigracionMovimientosHuevosCalculos.TryOperacion(tipoTexto, out var tipo))
            {
                errores.Add(new(fila.Numero, "Tipo", tipoTexto,
                    "Movimientos Huevos: tipo no reconocido. Usá 'Traslado' (a planta) o 'Venta'."));
                continue;
            }

            var itemTexto = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevosItem("Ítem")));
            if (itemTexto is null)
            { errores.Add(new(fila.Numero, "Ítem", null, "Movimientos Huevos: indicá el tipo de huevo que se mueve.")); continue; }
            if (!porClave.TryGetValue(MigracionCalculos.NormalizarClave(itemTexto), out var matches) || matches.Count == 0)
            {
                errores.Add(new(fila.Numero, "Ítem", itemTexto,
                    $"El tipo de huevo '{itemTexto}' no existe en el catálogo de huevo de la empresa (activo). Usá el nombre o el código de la hoja 'Referencias'."));
                continue;
            }
            if (matches.Count > 1)
            { errores.Add(new(fila.Numero, "Ítem", itemTexto, $"'{itemTexto}' coincide con {matches.Count} ítems; usá el código.")); continue; }

            var cantidad = EnteroNoNeg(fila, errores, "Cantidad", ClavesMovHuevosItem("Cantidad"));
            if (errores.Count > e0) continue;
            if (cantidad <= 0)
            { errores.Add(new(fila.Numero, "Cantidad", cantidad.ToString(), "Movimientos Huevos: la cantidad debe ser mayor a 0.")); continue; }

            var tipoDestinoTexto = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevosItem("Tipo Destino")));
            var tipoDestino = MigracionMovimientosHuevosCalculos.TipoDestinoEfectivo(tipoDestinoTexto, tipo);
            if (tipoDestino is null)
            {
                errores.Add(new(fila.Numero, "Tipo Destino", tipoDestinoTexto,
                    $"Movimientos Huevos: tipo de destino no reconocido. Usá {string.Join(", ", MigracionMovimientosHuevosCalculos.TiposDestino)} (vacío = default según el tipo)."));
                continue;
            }

            var destino = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevosItem("Destino")));
            var motivo = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevosItem("Motivo")));
            var descripcion = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevosItem("Descripción")));
            var observaciones = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovHuevosItem("Observaciones")));

            var claveGrupo = string.Join("|", fecha.Date.ToString("yyyy-MM-dd"), tipo.ToString(),
                tipoDestino, destino ?? "", motivo ?? "", descripcion ?? "");

            if (!grupos.TryGetValue(claveGrupo, out var grupo))
            {
                grupo = (new MovimientoHuevosMigFila(fila.Numero, fecha.Date, tipo, HuevosClasificacion.Cero,
                            tipoDestino, destino, motivo, descripcion, observaciones),
                         new List<HuevoItemSeguimientoDto>());
                grupos[claveGrupo] = grupo;
                orden.Add(claveGrupo);
            }

            var (id, codigo, nombre, tipoHuevo) = matches[0];
            var yaEsta = grupo.Items.FirstOrDefault(x => x.CatalogItemId == id);
            if (yaEsta is not null)
            {
                // Mismo ítem repetido dentro del mismo movimiento: se suma y se avisa, igual que en
                // la hoja "Huevos". No descarta la fila.
                errores.Add(new(fila.Numero, "Ítem", itemTexto,
                    $"'{nombre}' aparece más de una vez en el mismo movimiento: las cantidades se suman.", "Advertencia"));
                grupo.Items.Remove(yaEsta);
                cantidad += yaEsta.Cantidad;
            }
            grupo.Items.Add(new HuevoItemSeguimientoDto(
                CatalogItemId: id, Codigo: codigo, Nombre: nombre, TipoHuevo: tipoHuevo,
                Cantidad: cantidad, Um: "UND"));
        }

        return orden
            .Select(k => grupos[k])
            .Where(g => g.Items.Count > 0)
            .Select(g => g.Cabecera with { HuevoItems = g.Items })
            .ToList();

        void Indexar(string? texto, (int Id, string? Codigo, string Nombre, string? TipoHuevo) valor)
        {
            var clave = MigracionCalculos.NormalizarClave(texto);
            if (string.IsNullOrEmpty(clave)) return;
            if (!porClave.TryGetValue(clave, out var lista))
                porClave[clave] = lista = new List<(int, string?, string, string?)>();
            if (!lista.Any(x => x.Id == valor.Id)) lista.Add(valor);
        }
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
                    t.FechaTraslado, t.TipoOperacion, t.Metadata,
                    t.CantidadLimpio, t.CantidadTratado, t.CantidadSucio, t.CantidadDeforme, t.CantidadBlanco,
                    t.CantidadDobleYema, t.CantidadPiso, t.CantidadPequeno, t.CantidadRoto, t.CantidadDesecho, t.CantidadOtro
                })
                .ToListAsync(ct))
            .Where(t => MigracionMovimientosHuevosCalculos.TryOperacion(t.TipoOperacion, out _))
            .Select(t =>
            {
                MigracionMovimientosHuevosCalculos.TryOperacion(t.TipoOperacion, out var op);
                // Un traslado por ÍTEM tiene las 11 cantidades en cero: su identidad son los ítems del
                // metadata. Con la clave por categorías, dos movimientos distintos del mismo día
                // rendirían el MISMO string y el segundo se omitiría como "repetido".
                if (t.Metadata is not null)
                {
                    var items = HuevoItemsCalculos.LeerDeMetadata(t.Metadata.RootElement);
                    if (items.Count > 0)
                        return MigracionMovimientosHuevosCalculos.ClaveArchivoPorItems(
                            t.FechaTraslado.Date, op, items.Select(i => (i.CatalogItemId, i.Cantidad)));
                }
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
            var claveMovimiento = m.PorItems
                ? MigracionMovimientosHuevosCalculos.ClaveArchivoPorItems(
                    m.Fecha, m.Tipo, m.HuevoItems!.Select(i => (i.CatalogItemId, i.Cantidad)))
                : MigracionMovimientosHuevosCalculos.ClaveArchivo(m.Fecha, m.Tipo, m.Cantidades);
            if (clavesExistentes.Contains(claveMovimiento))
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
                    // Clasificación por ítems: las 11 quedan en 0 y el desglose real viaja en
                    // Metadata — byte a byte lo que hace el alta manual (TrasladoHuevosService, rama
                    // `usaHuevoItems`). El camino legacy queda idéntico.
                    CantidadLimpio = m.PorItems ? 0 : m.Cantidades.Limpio,
                    CantidadTratado = m.PorItems ? 0 : m.Cantidades.Tratado,
                    CantidadSucio = m.PorItems ? 0 : m.Cantidades.Sucio,
                    CantidadDeforme = m.PorItems ? 0 : m.Cantidades.Deforme,
                    CantidadBlanco = m.PorItems ? 0 : m.Cantidades.Blanco,
                    CantidadDobleYema = m.PorItems ? 0 : m.Cantidades.DobleYema,
                    CantidadPiso = m.PorItems ? 0 : m.Cantidades.Piso,
                    CantidadPequeno = m.PorItems ? 0 : m.Cantidades.Pequeno,
                    CantidadRoto = m.PorItems ? 0 : m.Cantidades.Roto,
                    CantidadDesecho = m.PorItems ? 0 : m.Cantidades.Desecho,
                    CantidadOtro = m.PorItems ? 0 : m.Cantidades.Otro,
                    // 🔴 `TotalHuevos` no se escribía nunca: la disponibilidad total del lote sale de
                    // esta columna, así que los movimientos de la carga masiva no la descontaban. Con
                    // ítems es además la ÚNICA cifra agregada que queda (las 11 van en cero).
                    TotalHuevos = m.Total,
                    Metadata = m.PorItems
                        ? HuevoItemsCalculos.EscribirEnMetadata(null, m.HuevoItems!.ToList())
                        : null,
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
    /// Disponibilidad proyectada POR ÍTEM del catálogo: lo disponible HOY (según el mismo servicio
    /// que consulta el traslado por pantalla) más lo que el archivo va a producir, menos lo que el
    /// archivo va a mover. Si algún ítem queda en negativo es un ERROR.
    ///
    /// <para>
    /// La versión por categorías no sirve acá: con clasificación por ítems las 11 columnas del espejo
    /// están en cero, así que compararía todo contra cero y rechazaría cualquier movimiento. Y la
    /// aritmética del saldo NO se reimplementa —
    /// <c>DisponibilidadLoteService.ObtenerDisponibilidadHuevoItemsLPPAsync</c> ya la tiene, leyendo
    /// el jsonb en vivo (producción menos traslados Completados) — acá solo se le suma el delta del
    /// archivo, que es lo único que ese servicio no puede saber.
    /// </para>
    ///
    /// <para>
    /// Sin la dependencia inyectada la validación se omite (mismo criterio que el resto de las
    /// opcionales del servicio); el importador igual no puede escribir un saldo negativo, porque el
    /// alta real de cada movimiento pasa por su propia validación.
    /// </para>
    /// </summary>
    private async Task ValidarDisponibilidadHuevosPorItemAsync(
        int loteId,
        IReadOnlyList<Dictionary<string, object?>> filasJson,
        IReadOnlyList<MovimientoHuevosMigFila> movimientosHuevos,
        List<MigracionErrorDto> errores,
        CancellationToken ct)
    {
        var aMover = movimientosHuevos.Where(m => m.PorItems).SelectMany(m => m.HuevoItems!).ToList();
        if (aMover.Count == 0 || _disponibilidad is null) return;

        var lppId = await _ctx.LotePosturaProduccion.AsNoTracking()
            .Where(x => x.LoteId == loteId && x.DeletedAt == null && x.LotePosturaProduccionId != null)
            .OrderByDescending(x => x.LotePosturaProduccionId)
            .Select(x => (int?)x.LotePosturaProduccionId!.Value)
            .FirstOrDefaultAsync(ct);
        if (lppId is null) return;

        var disponibleHoy = (await _disponibilidad.ObtenerDisponibilidadHuevoItemsLPPAsync(lppId.Value))
            .ToDictionary(i => i.CatalogItemId, i => i.Cantidad);

        // Lo que el archivo va a PRODUCIR: los ítems de la hoja "Huevos" ya parseados en el json de
        // cada día. Sin esto, cargar producción y movimientos en el mismo archivo se rechazaría solo.
        var produceElArchivo = new Dictionary<int, int>();
        foreach (var fila in filasJson)
        {
            if (!fila.TryGetValue("metadata", out var m) || m is not Dictionary<string, object?> meta) continue;
            if (!meta.TryGetValue(HuevoItemsCalculos.MetadataKey, out var lista)) continue;
            foreach (var item in EnumerarItems(lista))
                produceElArchivo[item.Id] = produceElArchivo.GetValueOrDefault(item.Id) + item.Cantidad;
        }

        var nombrePorId = aMover.Where(i => !string.IsNullOrWhiteSpace(i.Nombre))
            .GroupBy(i => i.CatalogItemId)
            .ToDictionary(g => g.Key, g => g.First().Nombre!);

        foreach (var grupo in aMover.GroupBy(i => i.CatalogItemId))
        {
            var movido = grupo.Sum(i => i.Cantidad);
            var hoy = disponibleHoy.GetValueOrDefault(grupo.Key);
            var delArchivo = produceElArchivo.GetValueOrDefault(grupo.Key);
            var proyectado = hoy + delArchivo - movido;
            if (proyectado >= 0) continue;

            var nombre = nombrePorId.GetValueOrDefault(grupo.Key, $"ítem {grupo.Key}");
            errores.Add(new(0, "Movimientos Huevos", nombre,
                $"No alcanzan los huevos de '{nombre}': disponibles {hoy} + {delArchivo} del archivo − {movido} a mover = {proyectado}. "
                + "Corregí las cantidades o cargá primero la producción de ese tipo de huevo."));
        }

        // El metadata que arma el parseo es un diccionario de objetos; se lee defensivamente porque
        // la forma la fija otro punto del código (SerializarItem) y no queremos acoplarnos a su tipo.
        static IEnumerable<(int Id, int Cantidad)> EnumerarItems(object? lista)
        {
            if (lista is not System.Collections.IEnumerable filas) yield break;
            foreach (var f in filas)
            {
                if (f is not IDictionary<string, object?> d) continue;
                var id = d.TryGetValue("catalogItemId", out var v) && v is int n ? n : 0;
                var cant = d.TryGetValue("cantidad", out var c) && c is int q ? q : 0;
                if (id > 0 && cant != 0) yield return (id, cant);
            }
        }
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
