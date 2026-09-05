// Infrastructure/Services/Funciones/ProduccionService.Seguimiento.cs — crear, actualizar y eliminar seguimiento diario de producción (merge con la fila de arrastre del levante y bloqueo atómico de inventario Colombia).
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using LoteDtos = ZooSanMarino.Application.DTOs.Lotes;
using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ProduccionService
{
    public async Task<int> CrearSeguimientoAsync(CrearSeguimientoRequest request)
    {
        if (!request.LotePosturaProduccionId.HasValue && !request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Debe especificar ProduccionLoteId o LotePosturaProduccionId.");
        if (request.LotePosturaProduccionId.HasValue && request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Especifique solo ProduccionLoteId o LotePosturaProduccionId, no ambos.");

        int loteId;
        int? lotePosturaProduccionId = request.LotePosturaProduccionId;

        // Fila del día creada por el arrastre de huevos del levante, si existe: habilita el MERGE
        // (sumar sobre ella) en vez del 400 por duplicado. Null ⇒ alta normal.
        SeguimientoProduccion? filaArrastre = null;

        if (lotePosturaProduccionId.HasValue)
        {
            var lpp = await _context.LotePosturaProduccion
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == lotePosturaProduccionId.Value
                    && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (lpp == null)
                throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");
            loteId = await ResolverYSanarLoteIdAsync(lpp);

            // La unicidad real en BD es (lote_id, fecha): si otro LPP comparte el mismo Lote base,
            // sin este OR el INSERT reventaría con violación de índice único (500) en vez de 400.
            // Se trae la FILA (no AnyAsync) porque si es la del arrastre de huevos del levante hay
            // que SUMARLE el seguimiento del día en vez de rechazarlo (ver ResolverFilaDuplicada).
            // Rango de día UTC en vez de `.Fecha.Date == ...`: EF traduce eso a
            // `date_trunc('day', fecha_registro) = @p`, y date_trunc sobre timestamptz trunca en la
            // zona de la SESIÓN de la BD ⇒ con una sesión no-UTC nunca casaba y el duplicado pasaba
            // sin detectarse. El rango es correcto en cualquier zona y además sargable.
            var (diaDesde, diaHasta) = FechasPuras.RangoDiaUtc(request.FechaRegistro);
            var existenteLpp = await _context.SeguimientoProduccion
                .FirstOrDefaultAsync(s => (s.LotePosturaProduccionId == lotePosturaProduccionId || s.LoteId == loteId)
                    && s.Fecha >= diaDesde && s.Fecha < diaHasta);
            filaArrastre = ResolverFilaDuplicada(existenteLpp, "Ya existe un seguimiento para esta fecha y lote.");
        }
        else
        {
            // Sin el filtro de empresa, cualquier usuario autenticado podía colgar un registro nuevo
            // del lote de OTRA empresa mandando su ProduccionLoteId (camino legacy, sin HasQueryFilter
            // global por CompanyId sobre Lote). Mismo criterio que la resolución por LotePosturaProduccionId
            // de arriba.
            var loteProd = await _context.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.ProduccionLoteId && l.Fase == "Produccion"
                    && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (loteProd == null)
                throw new ArgumentException("El registro de producción (lote en fase Producción) especificado no existe.");
            loteId = loteProd.LoteId ?? request.ProduccionLoteId!.Value;

            var (diaDesde, diaHasta) = FechasPuras.RangoDiaUtc(request.FechaRegistro);
            var existente = await _context.SeguimientoProduccion
                .FirstOrDefaultAsync(s => s.LoteId == loteId && s.Fecha >= diaDesde && s.Fecha < diaHasta);
            filaArrastre = ResolverFilaDuplicada(existente, "Ya existe un seguimiento para esta fecha.");
        }

        await EnsureLoteProduccionAbiertoAsync(loteId, lotePosturaProduccionId);

        // Corte de etapa: ese día no puede aportar consumo/bajas también desde levante (caso K345).
        await EnsureDiaSinAporteDeLevanteAsync(loteId, request);

        // Validar que la fecha no sea en el futuro
        if (request.FechaRegistro.Date > DateTime.Today)
        {
            throw new ArgumentException("La fecha de registro no puede ser en el futuro.");
        }

        decimal consumoKgH;
        decimal consumoKgM;
        JsonDocument? metadata;
        JsonDocument? itemsAdicionales = null;
        var tipoAlimento = request.TipoAlimento ?? string.Empty;

        var useItems = (request.ItemsHembras != null && request.ItemsHembras.Count > 0) ||
                       (request.ItemsMachos != null && request.ItemsMachos.Count > 0);

        if (useItems)
        {
            var (alimentosHembras, otrosHembras) = SepararAlimentosYOtrosItems(request.ItemsHembras);
            var (alimentosMachos, otrosMachos) = SepararAlimentosYOtrosItems(request.ItemsMachos);
            consumoKgH = (decimal)CalcularConsumoTotalAlimentos(alimentosHembras);
            consumoKgM = (decimal)CalcularConsumoTotalAlimentos(alimentosMachos);
            if (string.IsNullOrWhiteSpace(tipoAlimento))
                tipoAlimento = ConstruirTipoAlimentoString(request.ItemsHembras, request.ItemsMachos);
            metadata = BuildMetadataFromItems(request.ItemsHembras, request.ItemsMachos,
                request.ConsumoH, request.UnidadConsumoH, request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos);
            itemsAdicionales = BuildItemsAdicionales(otrosHembras, otrosMachos);
        }
        else
        {
            consumoKgH = 0;
            if (request.ConsumoH.HasValue && request.ConsumoH.Value > 0)
            {
                var unidadH = (request.UnidadConsumoH ?? "kg").ToLower().Trim();
                consumoKgH = unidadH == "g" || unidadH == "gramos" || unidadH == "gramo"
                    ? (decimal)(request.ConsumoH.Value / 1000.0)
                    : (decimal)request.ConsumoH.Value;
            }
            consumoKgM = 0;
            if (request.ConsumoM.HasValue && request.ConsumoM.Value > 0)
            {
                var unidadM = (request.UnidadConsumoM ?? "kg").ToLower().Trim();
                consumoKgM = unidadM == "g" || unidadM == "gramos" || unidadM == "gramo"
                    ? (decimal)(request.ConsumoM.Value / 1000.0)
                    : (decimal)request.ConsumoM.Value;
            }
            metadata = BuildMetadata(
                request.ConsumoH, request.UnidadConsumoH,
                request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos
            );
        }

        // ── Clasificación de huevos POR ÍTEMS (Santa Reyes) ───────────────────────────────
        // null o [] en creación = comportamiento actual intacto (11 columnas fijas del DTO).
        // Con ítems: se valida, se exige el flag de empresa, se guarda el desglose en el
        // metadata (conservando lo que ya escribió BuildMetadata*) y los totales salen de la suma.
        List<HuevoItemSeguimientoDto>? huevoItems = null;
        if (request.HuevoItems is { Count: > 0 })
        {
            huevoItems = await ValidarHuevoItemsAsync(loteId, request.HuevoItems, request.FechaRegistro).ConfigureAwait(false);
            metadata = HuevoItemsCalculos.EscribirEnMetadata(metadata, huevoItems);
        }

        // -- MERGE sobre la fila del arrastre de huevos del levante ------------------------
        // El usuario registra produccion el mismo dia en que se liquido el levante: sus huevos se
        // SUMAN a los que ya venian de levante y el resto de los campos los define su registro.
        // La marca se conserva para que el arrastre siga siendo idempotente.
        if (filaArrastre is not null)
        {
            // Se conserva la marca (para que el arrastre siga siendo idempotente) y se CIERRA la
            // ventana de merge: a partir de acá el día vuelve a admitir un solo registro.
            metadata = HuevosLevanteCalculos.CopiarMarcaArrastre(metadata, filaArrastre.Metadata);
            metadata = HuevosLevanteCalculos.MarcarSeguimientoRegistrado(metadata);
            AplicarRequestSobreFilaArrastre(filaArrastre, request, consumoKgH, consumoKgM,
                tipoAlimento, metadata);
        }

        var entity = filaArrastre ?? new SeguimientoProduccion
        {
            LoteId = loteId,
            LotePosturaProduccionId = lotePosturaProduccionId,
            // Anclada a MEDIODÍA: Npgsql legacy relee medianoche como el día ANTERIOR en Bogotá
            // (gotcha FechasPuras). Antes se guardaba request.FechaRegistro crudo.
            Fecha = FechasPuras.AnclarMediodiaUtc(request.FechaRegistro.Date),
            MortalidadH = request.MortalidadH,
            MortalidadM = request.MortalidadM,
            SelH = request.SelH,
            SelM = request.SelM,
            ErrorSexajeHembras = request.ErrorSexajeHembras ?? 0,
            ErrorSexajeMachos = request.ErrorSexajeMachos ?? 0,
            ConsKgH = consumoKgH,
            ConsKgM = consumoKgM,
            HuevoTot = request.HuevosTotales,
            HuevoInc = request.HuevosIncubables,
            HuevoLimpio = request.HuevoLimpio,
            HuevoTratado = request.HuevoTratado,
            HuevoSucio = request.HuevoSucio,
            HuevoDeforme = request.HuevoDeforme,
            HuevoBlanco = request.HuevoBlanco,
            HuevoDobleYema = request.HuevoDobleYema,
            HuevoPiso = request.HuevoPiso,
            HuevoPequeno = request.HuevoPequeno,
            HuevoRoto = request.HuevoRoto,
            HuevoDesecho = request.HuevoDesecho,
            HuevoOtro = request.HuevoOtro,
            TipoAlimento = tipoAlimento,
            Observaciones = request.Observaciones,
            PesoHuevo = request.PesoHuevo,
            Etapa = request.Etapa,
            PesoH = request.PesoH,
            PesoM = request.PesoM,
            Uniformidad = request.Uniformidad,
            CoeficienteVariacion = request.CoeficienteVariacion,
            UniformidadHembras = request.UniformidadHembras,
            UniformidadMachos = request.UniformidadMachos,
            CvHembras = request.CvHembras,
            CvMachos = request.CvMachos,
            Ciclo = request.Ciclo,
            ObservacionesPesaje = request.ObservacionesPesaje,
            Metadata = metadata,
            ConsumoAguaDiario = request.ConsumoAguaDiario,
            ConsumoAguaPh = request.ConsumoAguaPh,
            ConsumoAguaOrp = request.ConsumoAguaOrp,
            ConsumoAguaTemperatura = request.ConsumoAguaTemperatura,
            CompanyId = _currentUser.CompanyId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        // huevo_tot = suma de los ítems; huevo_inc y las 11 columnas fijas quedan en 0.
        if (huevoItems != null) AplicarTotalesHuevoPorItems(entity, huevoItems);

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO (Fase 3 paso 2) ────────────
        // Descuento desde los DTOs del request (TODOS los ítems), id-mapping catalogItemId→ítem B.
        // Validación previa de stock B ANTES de persistir; guardado + consumo en UNA tx. Si falta
        // stock/ítem → throw por ítem → rollback → NO se guarda el seguimiento.
        var (granjaId, paisId, modelo) = await ResolverGranjaYModeloAsync(loteId);

        // ── Doble validación ───────────────────────────────────────────────────────────────────
        // Con la empresa en doble validación no se descuenta al guardar: se separa. Con el flag
        // apagado `separa` queda en false y todo lo que sigue corre igual que antes.
        var separa = _validacion is not null
                  && ValidacionSeguimientoCalculos.SeparaAlGuardar(await _validacion.RequiereValidacionAsync());

        // Sin granja resuelta no hay separación posible: `farm_id` es NOT NULL con FK a `farms`, así que
        // `granjaId ?? 0` revienta con 23503 y el usuario ve un 500 opaco. Pasa de verdad —un LPP vivo
        // cuyo lote base está soft-deleted resuelve (null, null, Ninguno)—. Y si la FK no estuviera,
        // sería peor: una reserva sin ubicación que al validar no descuenta nada.
        if (separa && granjaId is not > 0)
            throw new InvalidOperationException(
                "No se puede registrar el seguimiento: no se pudo resolver la granja del lote. " +
                "Sin granja, lo que se separe no tiene ubicación contra la cual descontar al validar. " +
                "Verificá que el lote base exista y esté activo.");

        if (separa)
        {
            await _validacion!.AsegurarPuedeRegistrarDiaAsync(
                ModuloSeguimiento.Produccion, lotePosturaProduccionId ?? loteId);
            // Los kilos ya normalizados, no `request.ConsumoH`: el request trae la cantidad CON unidad
            // y puede venir en gramos. Sin ellos, el cliente que manda el consumo como campo suelto
            // (móvil, carga masiva, PWA) contaba cero y se comía un 400 «no tiene alimento».
            SeparacionSeguimientoHelper.ValidarAlimentoObligatorio(
                ModuloSeguimiento.Produccion, loteEsMixto: false, metadata, request.FechaRegistro,
                consumoKgH, consumoKgM);
        }

        if (!separa && modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && granjaId is > 0 && useItems)
        {
            var byItem = AcumularItemsRequestPorOrigen(request.ItemsHembras, request.ItemsMachos);
            var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

            await _colombiaConsumoB.ValidarStockConsumoAsync(granjaId.Value, positivos, loteId); // lanza si falta (antes de persistir)

            // Transaccion CONDICIONAL: null cuando ya hay una ambiente (push offline de la PWA). EF lanza
            // si se abre una segunda sobre el mismo contexto. Sin ambiente abre la suya y el
            // comportamiento es identico al de antes.
            await using var tx = _context.Database.CurrentTransaction is null
                ? await _context.Database.BeginTransactionAsync()
                : null;
            if (filaArrastre is null) _context.SeguimientoProduccion.Add(entity);
            await _context.SaveChangesAsync();
            if (positivos.Count > 0)
            {
                var refStr = $"Seguimiento producción #{entity.Id} {request.FechaRegistro:yyyy-MM-dd}";
                await _colombiaConsumoB.AplicarConsumoAsync(granjaId.Value, positivos, refStr, fechaMovimiento: request.FechaRegistro);
                await _context.SaveChangesAsync();
            }
            if (tx is not null) await tx.CommitAsync();
            if (lotePosturaProduccionId.HasValue)
                await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId.Value).ConfigureAwait(false);
            return entity.Id;
        }

        // `validado` significa «su efecto ya se aplicó», no «alguien apretó el botón». Con el flag
        // apagado el registro descuenta AL GUARDAR, así que nace validado. Dejarlo en el default
        // (false) hacía que el día que la empresa encendiera la doble validación todos los registros
        // creados desde el backfill aparecieran pendientes, pasaran a EN RETRASO a las 24 h y
        // bloquearan el alta de días nuevos —sin tener nada que validar—.
        entity.Validado = !separa;

        if (filaArrastre is null) _context.SeguimientoProduccion.Add(entity);
        await _context.SaveChangesAsync();

        // La separación va DESPUÉS de persistir: necesita el id para poder liberarla o aplicarla.
        if (separa)
        {
            // El país va RESUELTO (columna del lote o, si viene vacía, granja→departamento→país). Es el
            // que la reserva persiste y con el que se elige el modelo de inventario al validar: con
            // `null` la validación resolvía `Ninguno`, se saltaba el descuento y aun así marcaba el
            // registro validado y la reserva aplicada. El alimento no se descontaba nunca.
            await _validacion!.SepararAsync(SeparacionSeguimientoHelper.Contexto(
                ModuloSeguimiento.Produccion, entity.Id, paisId,
                granjaId ?? 0, null, null,
                lotePosturaProduccionId ?? loteId, loteId.ToString(), request.FechaRegistro, metadata,
                entity.MortalidadH, entity.SelH, entity.ErrorSexajeHembras,
                entity.MortalidadM, entity.SelM, entity.ErrorSexajeMachos,
                poblacionEsMixta: false));
        }

        if (lotePosturaProduccionId.HasValue)
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId.Value).ConfigureAwait(false);
        return entity.Id;
    }

    /// <summary>
    /// Politica de duplicado por dia. Devuelve la fila SOLO si es la que creo el arrastre de huevos
    /// del levante Y todavia no se registro el seguimiento de ese dia, habilitando el merge
    /// acumulativo UNA sola vez (la regla "un registro por dia" se conserva).
    /// En cualquier otro caso lanza con el mensaje historico, es decir el 400 de siempre para todos
    /// los casos que ya existian (filas manuales, de traslado de aves, etc.).
    /// </summary>
    private static SeguimientoProduccion? ResolverFilaDuplicada(SeguimientoProduccion? existente, string mensaje)
    {
        if (existente is null) return null;
        if (HuevosLevanteCalculos.PermiteMergeSeguimiento(existente.Metadata)) return existente;
        throw new InvalidOperationException(mensaje);
    }

    /// <summary>
    /// Vuelca el request sobre la fila del arrastre: los huevos se SUMAN categoria por categoria
    /// (recalculando <c>huevo_tot</c>/<c>huevo_inc</c> desde el resultado) y el resto de los campos
    /// se reemplazan por lo que registro el usuario. No toca <c>traslado_*</c>, ni
    /// <c>lote_id</c>/<c>fecha_registro</c>/auditoria de creacion.
    /// </summary>
    private void AplicarRequestSobreFilaArrastre(
        SeguimientoProduccion fila,
        CrearSeguimientoRequest request,
        decimal consumoKgH,
        decimal consumoKgM,
        string tipoAlimento,
        JsonDocument? metadata)
    {
        var sumado = HuevosLevanteCalculos.Sumar(
            new HuevosClasificacion(
                Limpio: fila.HuevoLimpio,
                Tratado: fila.HuevoTratado,
                Sucio: fila.HuevoSucio,
                Deforme: fila.HuevoDeforme,
                Blanco: fila.HuevoBlanco,
                DobleYema: fila.HuevoDobleYema,
                Piso: fila.HuevoPiso,
                Pequeno: fila.HuevoPequeno,
                Roto: fila.HuevoRoto,
                Desecho: fila.HuevoDesecho,
                Otro: fila.HuevoOtro),
            new HuevosClasificacion(
                Limpio: request.HuevoLimpio,
                Tratado: request.HuevoTratado,
                Sucio: request.HuevoSucio,
                Deforme: request.HuevoDeforme,
                Blanco: request.HuevoBlanco,
                DobleYema: request.HuevoDobleYema,
                Piso: request.HuevoPiso,
                Pequeno: request.HuevoPequeno,
                Roto: request.HuevoRoto,
                Desecho: request.HuevoDesecho,
                Otro: request.HuevoOtro));

        fila.HuevoLimpio = sumado.Limpio;
        fila.HuevoTratado = sumado.Tratado;
        fila.HuevoSucio = sumado.Sucio;
        fila.HuevoDeforme = sumado.Deforme;
        fila.HuevoBlanco = sumado.Blanco;
        fila.HuevoDobleYema = sumado.DobleYema;
        fila.HuevoPiso = sumado.Piso;
        fila.HuevoPequeno = sumado.Pequeno;
        fila.HuevoRoto = sumado.Roto;
        fila.HuevoDesecho = sumado.Desecho;
        fila.HuevoOtro = sumado.Otro;
        // Derivados desde las 11 categorias ya sumadas (no se suman los totales del request aparte,
        // para que no puedan quedar descuadrados). Con clasificacion por items, el
        // AplicarTotalesHuevoPorItems posterior manda.
        fila.HuevoInc = sumado.Incubables;
        fila.HuevoTot = sumado.Totales;

        fila.MortalidadH = request.MortalidadH;
        fila.MortalidadM = request.MortalidadM;
        fila.SelH = request.SelH;
        fila.SelM = request.SelM;
        fila.ErrorSexajeHembras = request.ErrorSexajeHembras ?? 0;
        fila.ErrorSexajeMachos = request.ErrorSexajeMachos ?? 0;
        fila.ConsKgH = consumoKgH;
        fila.ConsKgM = consumoKgM;
        fila.TipoAlimento = tipoAlimento;
        fila.Etapa = request.Etapa;
        if (request.PesoHuevo > 0) fila.PesoHuevo = request.PesoHuevo;
        fila.PesoH = request.PesoH ?? fila.PesoH;
        fila.PesoM = request.PesoM ?? fila.PesoM;
        fila.Uniformidad = request.Uniformidad ?? fila.Uniformidad;
        fila.CoeficienteVariacion = request.CoeficienteVariacion ?? fila.CoeficienteVariacion;
        fila.UniformidadHembras = request.UniformidadHembras ?? fila.UniformidadHembras;
        fila.UniformidadMachos = request.UniformidadMachos ?? fila.UniformidadMachos;
        fila.CvHembras = request.CvHembras ?? fila.CvHembras;
        fila.CvMachos = request.CvMachos ?? fila.CvMachos;
        fila.Ciclo = request.Ciclo ?? fila.Ciclo;
        fila.ObservacionesPesaje = request.ObservacionesPesaje ?? fila.ObservacionesPesaje;
        fila.ConsumoAguaDiario = request.ConsumoAguaDiario ?? fila.ConsumoAguaDiario;
        fila.ConsumoAguaPh = request.ConsumoAguaPh ?? fila.ConsumoAguaPh;
        fila.ConsumoAguaOrp = request.ConsumoAguaOrp ?? fila.ConsumoAguaOrp;
        fila.ConsumoAguaTemperatura = request.ConsumoAguaTemperatura ?? fila.ConsumoAguaTemperatura;
        fila.Observaciones = string.IsNullOrWhiteSpace(request.Observaciones)
            ? fila.Observaciones
            : (string.IsNullOrWhiteSpace(fila.Observaciones)
                ? request.Observaciones
                : $"{fila.Observaciones} | {request.Observaciones}");
        fila.Metadata = metadata;
        fila.UpdatedByUserId = _currentUser.UserId;
        fila.UpdatedAt = DateTime.UtcNow;
    }

    public async Task ActualizarSeguimientoAsync(int id, CrearSeguimientoRequest request)
    {
        if (!request.LotePosturaProduccionId.HasValue && !request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Debe especificar ProduccionLoteId o LotePosturaProduccionId.");
        if (request.LotePosturaProduccionId.HasValue && request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Especifique solo ProduccionLoteId o LotePosturaProduccionId, no ambos.");

        int loteId;
        int? lotePosturaProduccionId = request.LotePosturaProduccionId;

        if (lotePosturaProduccionId.HasValue)
        {
            var lpp = await _context.LotePosturaProduccion.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == lotePosturaProduccionId.Value
                    && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (lpp == null)
                throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");
            loteId = await ResolverYSanarLoteIdAsync(lpp);
        }
        else
        {
            // Mismo filtro que en el alta: sin `CompanyId == _currentUser.CompanyId` acá, un usuario
            // podía mandar el ProduccionLoteId de un lote de OTRA empresa y mover su propio registro
            // (ya validado como suyo más abajo por `esDeMiEmpresa`, pero contra el lote ORIGINAL) para
            // que quede colgando del lote ajeno — la reasignación de `entity.LoteId` nunca se validaba.
            var loteProd = await _context.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.ProduccionLoteId && l.Fase == "Produccion"
                    && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (loteProd == null)
                throw new ArgumentException("El registro de producción (lote en fase Producción) especificado no existe.");
            loteId = loteProd.LoteId ?? request.ProduccionLoteId!.Value;
        }

        await EnsureLoteProduccionAbiertoAsync(loteId, lotePosturaProduccionId);

        if (request.FechaRegistro.Date > DateTime.Today)
            throw new ArgumentException("La fecha de registro no puede ser en el futuro.");

        decimal consumoKgH;
        decimal consumoKgM;
        JsonDocument? metadata;
        JsonDocument? itemsAdicionales = null;
        var tipoAlimento = request.TipoAlimento ?? string.Empty;

        var useItems = (request.ItemsHembras != null && request.ItemsHembras.Count > 0) ||
                       (request.ItemsMachos != null && request.ItemsMachos.Count > 0);

        if (useItems)
        {
            var (alimentosHembras, otrosHembras) = SepararAlimentosYOtrosItems(request.ItemsHembras);
            var (alimentosMachos, otrosMachos) = SepararAlimentosYOtrosItems(request.ItemsMachos);
            consumoKgH = (decimal)CalcularConsumoTotalAlimentos(alimentosHembras);
            consumoKgM = (decimal)CalcularConsumoTotalAlimentos(alimentosMachos);
            if (string.IsNullOrWhiteSpace(tipoAlimento))
                tipoAlimento = ConstruirTipoAlimentoString(request.ItemsHembras, request.ItemsMachos);
            metadata = BuildMetadataFromItems(request.ItemsHembras, request.ItemsMachos,
                request.ConsumoH, request.UnidadConsumoH, request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos);
            itemsAdicionales = BuildItemsAdicionales(otrosHembras, otrosMachos);
        }
        else
        {
            consumoKgH = 0;
            if (request.ConsumoH.HasValue && request.ConsumoH.Value > 0)
            {
                var unidadH = (request.UnidadConsumoH ?? "kg").ToLowerInvariant().Trim();
                consumoKgH = unidadH == "g" || unidadH == "gramos" || unidadH == "gramo"
                    ? (decimal)(request.ConsumoH.Value / 1000.0)
                    : (decimal)request.ConsumoH.Value;
            }
            consumoKgM = 0;
            if (request.ConsumoM.HasValue && request.ConsumoM.Value > 0)
            {
                var unidadM = (request.UnidadConsumoM ?? "kg").ToLowerInvariant().Trim();
                consumoKgM = unidadM == "g" || unidadM == "gramos" || unidadM == "gramo"
                    ? (decimal)(request.ConsumoM.Value / 1000.0)
                    : (decimal)request.ConsumoM.Value;
            }
            metadata = BuildMetadata(
                request.ConsumoH, request.UnidadConsumoH,
                request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos
            );
        }

        var entity = await _context.SeguimientoProduccion
            .FirstOrDefaultAsync(x => x.Id == id)
            .ConfigureAwait(false);
        if (entity == null)
            throw new InvalidOperationException("No se encontró el registro o no tiene permisos para actualizarlo.");

        // La fila editada debe ser de la empresa activa (mismo cierre isMine que Obtener/Eliminar;
        // antes la edición no validaba el dueño de la fila original).
        var esDeMiEmpresa = await _context.Lotes.AsNoTracking()
            .AnyAsync(l => l.LoteId == entity.LoteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .ConfigureAwait(false);
        if (!esDeMiEmpresa)
            throw new InvalidOperationException("No se encontró el registro o no tiene permisos para actualizarlo.");

        // La edición puede cambiar fecha y/o lote: re-validar que no quede OTRO registro del
        // mismo día calendario (devuelve el 400 histórico en vez del 500 por índice único).
        var (diaDesdeEd, diaHastaEd) = FechasPuras.RangoDiaUtc(request.FechaRegistro);
        var duplicadoDia = await _context.SeguimientoProduccion.AsNoTracking()
            .AnyAsync(s => s.Id != id
                && (s.LoteId == loteId
                    || (lotePosturaProduccionId.HasValue && s.LotePosturaProduccionId == lotePosturaProduccionId))
                && s.Fecha >= diaDesdeEd && s.Fecha < diaHastaEd)
            .ConfigureAwait(false);
        if (duplicadoDia)
            throw new InvalidOperationException("Ya existe un seguimiento para esta fecha y lote.");

        // Fase 2 (S4) — capturar el consumo ANTERIOR (desde el metadata guardado) ANTES de pisarlo,
        // para calcular el diff old/new en el descuento Colombia. Parseo TIPADO (conserva el
        // origen del id, camino 1/2) — solo la rama Colombia consume este diccionario.
        var oldByItemId = entity.Metadata != null
            ? MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(entity.Metadata.RootElement)
            : new Dictionary<ItemConsumoKey, decimal>();

        // ── Clasificación de huevos POR ÍTEMS (Santa Reyes) — edición ────────────────────
        //   null  = "no tocar": se conserva el desglose ya persistido (y sus totales), NO se pisa
        //           con los campos sueltos del DTO;
        //   []    = "quitar la clasificación por ítems": se elimina la clave del metadata y los
        //           totales vuelven a salir de los campos sueltos, como hoy;
        //   [..]  = reemplaza el desglose (se revalida) y recalcula huevo_tot / huevo_inc / 11 columnas.
        var huevoItemsPersistidos = entity.Metadata != null
            ? HuevoItemsCalculos.LeerDeMetadata(entity.Metadata.RootElement)
            : new List<HuevoItemSeguimientoDto>();

        List<HuevoItemSeguimientoDto>? huevoItems = null;
        if (request.HuevoItems is null)
        {
            if (huevoItemsPersistidos.Count > 0) huevoItems = huevoItemsPersistidos;
        }
        else if (request.HuevoItems.Count > 0)
        {
            huevoItems = await ValidarHuevoItemsAsync(loteId, request.HuevoItems, request.FechaRegistro).ConfigureAwait(false);
        }

        if (huevoItems != null)
            metadata = HuevoItemsCalculos.EscribirEnMetadata(metadata, huevoItems);

        // Conservar la marca de arrastre de huevos del levante si la fila la tenía: la edición
        // reconstruye el metadata desde el request y antes la PERDÍA (el re-arrastre dejaba de
        // ser idempotente y volvía a sumar todo desde cero).
        metadata = HuevosLevanteCalculos.CopiarMarcaArrastre(metadata, entity.Metadata);

        entity.LoteId = loteId;
        entity.LotePosturaProduccionId = lotePosturaProduccionId;
        // Anclada a MEDIODÍA (gotcha Npgsql legacy / FechasPuras), antes se guardaba cruda.
        entity.Fecha = FechasPuras.AnclarMediodiaUtc(request.FechaRegistro.Date);
        entity.MortalidadH = request.MortalidadH;
        entity.MortalidadM = request.MortalidadM;
        entity.SelH = request.SelH;
        entity.SelM = request.SelM;
        entity.ErrorSexajeHembras = request.ErrorSexajeHembras ?? 0;
        entity.ErrorSexajeMachos = request.ErrorSexajeMachos ?? 0;
        entity.ConsKgH = consumoKgH;
        entity.ConsKgM = consumoKgM;
        entity.HuevoTot = request.HuevosTotales;
        entity.HuevoInc = request.HuevosIncubables;
        entity.HuevoLimpio = request.HuevoLimpio;
        entity.HuevoTratado = request.HuevoTratado;
        entity.HuevoSucio = request.HuevoSucio;
        entity.HuevoDeforme = request.HuevoDeforme;
        entity.HuevoBlanco = request.HuevoBlanco;
        entity.HuevoDobleYema = request.HuevoDobleYema;
        entity.HuevoPiso = request.HuevoPiso;
        entity.HuevoPequeno = request.HuevoPequeno;
        entity.HuevoRoto = request.HuevoRoto;
        entity.HuevoDesecho = request.HuevoDesecho;
        entity.HuevoOtro = request.HuevoOtro;
        entity.TipoAlimento = tipoAlimento;
        entity.Observaciones = request.Observaciones;
        entity.PesoHuevo = request.PesoHuevo;
        entity.Etapa = request.Etapa;
        entity.PesoH = request.PesoH;
        entity.PesoM = request.PesoM;
        entity.Uniformidad = request.Uniformidad;
        entity.CoeficienteVariacion = request.CoeficienteVariacion;
        entity.UniformidadHembras = request.UniformidadHembras;
        entity.UniformidadMachos = request.UniformidadMachos;
        entity.CvHembras = request.CvHembras;
        entity.CvMachos = request.CvMachos;
        entity.Ciclo = request.Ciclo;
        entity.ObservacionesPesaje = request.ObservacionesPesaje;
        entity.Metadata = metadata;
        entity.ConsumoAguaDiario = request.ConsumoAguaDiario;
        entity.ConsumoAguaPh = request.ConsumoAguaPh;
        entity.ConsumoAguaOrp = request.ConsumoAguaOrp;
        entity.ConsumoAguaTemperatura = request.ConsumoAguaTemperatura;
        entity.UpdatedByUserId = _currentUser.UserId;
        entity.UpdatedAt = DateTime.UtcNow;

        // huevo_tot = suma de los ítems; huevo_inc y las 11 columnas fijas quedan en 0.
        if (huevoItems != null) AplicarTotalesHuevoPorItems(entity, huevoItems);

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO en edición (Fase 3 paso 2) ──
        // diff old/new por catalogItemId (id-mapping A→B): diff>0 = consumo adicional; diff<0 = devolución.
        // Validación previa del stock B de los diff POSITIVOS ANTES de persistir; save + diff en UNA tx.
        var (granjaId, paisId, modelo) = await ResolverGranjaYModeloAsync(loteId);

        // ── Doble validación ───────────────────────────────────────────────────────────────────
        // La edición NO puede aplicar el diff de inventario cuando la empresa separa: el alta solo
        // reservó, así que aplicar acá descontaría kilos que después la validación vuelve a descontar
        // leyendo la reserva. Con el flag ON se reescribe la reserva y listo — que es toda la ventaja
        // del modelo: como nunca se descontó, editar no necesita calcular `nuevo − viejo`.
        var separaEd = _validacion is not null
                    && ValidacionSeguimientoCalculos.SeparaAlGuardar(await _validacion.RequiereValidacionAsync());

        // Sin granja resuelta no hay separación posible: `farm_id` es NOT NULL con FK a `farms`, así que
        // `granjaId ?? 0` revienta con 23503 y el usuario ve un 500 opaco. Pasa de verdad —un LPP vivo
        // cuyo lote base está soft-deleted resuelve (null, null, Ninguno)—. Y si la FK no estuviera,
        // sería peor: una reserva sin ubicación que al validar no descuenta nada.
        if (separaEd && granjaId is not > 0)
            throw new InvalidOperationException(
                "No se puede registrar el seguimiento: no se pudo resolver la granja del lote. " +
                "Sin granja, lo que se separe no tiene ubicación contra la cual descontar al validar. " +
                "Verificá que el lote base exista y esté activo.");

        if (separaEd)
        {
            if (!ValidacionSeguimientoCalculos.EsEditable(true, entity.Validado))
                throw new InvalidOperationException(
                    ValidacionSeguimientoCalculos.MensajeRegistroValidado("editar"));

            SeparacionSeguimientoHelper.ValidarAlimentoObligatorio(
                ModuloSeguimiento.Produccion, loteEsMixto: false, metadata, request.FechaRegistro,
                consumoKgH, consumoKgM);
        }

        if (!separaEd && modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && granjaId is > 0)
        {
            var newByItemId = AcumularItemsRequestPorOrigen(request.ItemsHembras, request.ItemsMachos);
            var incrementos = new Dictionary<ItemConsumoKey, decimal>();
            var allKeys = new HashSet<ItemConsumoKey>(oldByItemId.Keys);
            foreach (var k in newByItemId.Keys) allKeys.Add(k);
            foreach (var key in allKeys)
            {
                var diff = newByItemId.GetValueOrDefault(key) - oldByItemId.GetValueOrDefault(key);
                if (diff > 0) incrementos[key] = diff;
            }
            await _colombiaConsumoB.ValidarStockConsumoAsync(granjaId.Value, incrementos, loteId); // lanza si falta (antes de persistir)

            // Transaccion CONDICIONAL: null cuando ya hay una ambiente (push offline de la PWA). EF lanza
            // si se abre una segunda sobre el mismo contexto. Sin ambiente abre la suya y el
            // comportamiento es identico al de antes.
            await using var tx = _context.Database.CurrentTransaction is null
                ? await _context.Database.BeginTransactionAsync()
                : null;
            var refStr = $"Seguimiento producción #{entity.Id} {request.FechaRegistro:yyyy-MM-dd}";
            await _colombiaConsumoB.AplicarDiffAsync(granjaId.Value, oldByItemId, newByItemId, refStr, fechaMovimiento: request.FechaRegistro);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            if (tx is not null) await tx.CommitAsync();
            if (lotePosturaProduccionId.HasValue)
                await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId.Value).ConfigureAwait(false);
            return;
        }

        await _context.SaveChangesAsync().ConfigureAwait(false);

        // Reescribir la reserva va DESPUÉS de persistir: `SepararAsync` libera lo que el registro
        // tuviera activo y escribe lo nuevo, así que la edición queda cubierta sin diff.
        if (separaEd)
        {
            await _validacion!.SepararAsync(SeparacionSeguimientoHelper.Contexto(
                ModuloSeguimiento.Produccion, entity.Id, paisId,
                granjaId ?? 0, null, null,
                lotePosturaProduccionId ?? loteId, loteId.ToString(), request.FechaRegistro, metadata,
                entity.MortalidadH, entity.SelH, entity.ErrorSexajeHembras,
                entity.MortalidadM, entity.SelM, entity.ErrorSexajeMachos,
                poblacionEsMixta: false));
        }

        if (lotePosturaProduccionId.HasValue)
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Elimina un seguimiento diario de producción. Fase 3 (paso 2): para lotes Colombia (modelo B
    /// nivel granja) devuelve el stock consumido (Ingreso total) y el borrado + la devolución van
    /// en UNA transacción (todo-o-nada). Ecuador/Panamá no usan esta ruta de inventario.
    /// </summary>
    public async Task<bool> EliminarSeguimientoAsync(int seguimientoId)
    {
        var e = await _context.SeguimientoProduccion
            .FirstOrDefaultAsync(x => x.Id == seguimientoId)
            .ConfigureAwait(false);
        if (e == null) return false;

        var isMine = await _context.Lotes.AsNoTracking()
            .AnyAsync(l => l.LoteId == e.LoteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .ConfigureAwait(false);
        if (!isMine) return false;

        var lppId = e.LotePosturaProduccionId;
        var loteId = e.LoteId;

        await EnsureLoteProduccionAbiertoAsync(loteId, lppId);

        // ── Doble validación ───────────────────────────────────────────────────────────────────
        // Con el flag ON el registro nunca descontó: solo separó. Devolver stock acá sería INFLAR el
        // inventario con kilos que jamás salieron. Lo correcto es liberar la reserva — y sin eso el
        // disponible quedaba comprometido para siempre por un registro que ya no existe.
        var separaDel = _validacion is not null
                     && ValidacionSeguimientoCalculos.SeparaAlGuardar(await _validacion.RequiereValidacionAsync());
        if (separaDel)
        {
            if (!ValidacionSeguimientoCalculos.EsEditable(true, e.Validado))
                throw new InvalidOperationException(
                    ValidacionSeguimientoCalculos.MensajeRegistroValidado("eliminar"));

            await _validacion!.LiberarAsync(ModuloSeguimiento.Produccion, seguimientoId);

            _context.SeguimientoProduccion.Remove(e);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            if (lppId.HasValue)
                await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lppId.Value).ConfigureAwait(false);
            return true;
        }

        var (granjaId, _, modelo) = await ResolverGranjaYModeloAsync(loteId);
        if (modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && granjaId is > 0)
        {
            var byItem = e.Metadata != null
                ? MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(e.Metadata.RootElement)
                : new Dictionary<ItemConsumoKey, decimal>();
            var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

            // Transaccion CONDICIONAL: null cuando ya hay una ambiente (push offline de la PWA). EF lanza
            // si se abre una segunda sobre el mismo contexto. Sin ambiente abre la suya y el
            // comportamiento es identico al de antes.
            await using var tx = _context.Database.CurrentTransaction is null
                ? await _context.Database.BeginTransactionAsync()
                : null;
            if (positivos.Count > 0)
            {
                var refStr = $"Seguimiento producción #{seguimientoId} (devolución por eliminación)";
                // Fecha = día del BORRADO (hecho de HOY), no la fecha del seguimiento eliminado.
                await _colombiaConsumoB.AplicarDevolucionAsync(granjaId.Value, positivos, refStr, "Devolución por eliminación de seguimiento producción", fechaMovimiento: DateTime.UtcNow.Date);
            }
            _context.SeguimientoProduccion.Remove(e);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            if (tx is not null) await tx.CommitAsync();
            if (lppId.HasValue)
                await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lppId.Value).ConfigureAwait(false);
            return true;
        }

        _context.SeguimientoProduccion.Remove(e);
        await _context.SaveChangesAsync().ConfigureAwait(false);
        if (lppId.HasValue)
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lppId.Value).ConfigureAwait(false);
        return true;
    }
}
