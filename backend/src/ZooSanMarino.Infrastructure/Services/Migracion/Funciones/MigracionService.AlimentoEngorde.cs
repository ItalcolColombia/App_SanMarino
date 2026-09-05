// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.AlimentoEngorde.cs
// Hoja "Alimento" del archivo de seguimiento pollo engorde: movimientos de INVENTARIO (ingresos,
// traslados y recepción de tránsito) que se aplican ANTES de las filas de consumo de la hoja "Datos".
//
// Por qué existe: el operario tenía dos archivos — las llegadas de alimento al galpón por un lado y el
// seguimiento diario por otro — y el inventario nunca cuadraba, porque el consumo del seguimiento
// descuenta de un stock que nadie había cargado. Con esta hoja, un solo archivo deja el galpón con el
// saldo correcto.
//
// La lógica de inventario NO se reimplementa: cada fila delega en IInventarioGestionService, el mismo
// servicio que usa la pantalla de gestión (misma validación de pertenencia, mismo histórico, mismos
// estados de movimiento).
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    /// <summary>Una fila de la hoja "Alimento" ya validada y resuelta a ids.</summary>
    private sealed record MovimientoAlimentoFila(
        int Numero,
        MovimientoAlimento Movimiento,
        DateTime Fecha,
        int ItemId,
        string ItemNombre,
        decimal CantidadKg,
        UbicacionAlimento Destino,
        UbicacionAlimento? Origen,
        string OrigenTipo,
        string? Referencia,
        string? Observaciones);

    /// <summary>Ubicaciones de una granja resueltas por nombre o código, para las columnas de la hoja.</summary>
    private sealed record GranjaUbicaciones(
        int FarmId,
        string Nombre,
        Dictionary<string, string> NucleosPorClave,
        Dictionary<string, string> GalponesPorClave);

    /// <summary>
    /// Granjas de la empresa con sus núcleos y galpones indexados por clave normalizada (código Y
    /// nombre), para resolver las columnas Granja/Núcleo/Galpón de la hoja tal como las escribe el
    /// usuario.
    /// </summary>
    private async Task<Dictionary<string, List<GranjaUbicaciones>>> CargarUbicacionesEmpresaAsync(int companyId, CancellationToken ct)
    {
        var granjas = await _ctx.Farms.AsNoTracking()
            .Where(f => f.CompanyId == companyId)
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(ct);
        var ids = granjas.Select(g => g.Id).ToList();

        var nucleos = await _ctx.Nucleos.AsNoTracking()
            .Where(n => ids.Contains(n.GranjaId))
            .Select(n => new { n.GranjaId, n.NucleoId, n.NucleoNombre })
            .ToListAsync(ct);
        var galpones = await _ctx.Galpones.AsNoTracking()
            .Where(g => ids.Contains(g.GranjaId))
            .Select(g => new { g.GranjaId, g.GalponId, g.GalponNombre, g.NucleoId })
            .ToListAsync(ct);

        var porNombre = new Dictionary<string, List<GranjaUbicaciones>>();
        foreach (var g in granjas)
        {
            var nuc = new Dictionary<string, string>();
            foreach (var n in nucleos.Where(x => x.GranjaId == g.Id))
            {
                Indexar(nuc, n.NucleoId, n.NucleoId);
                Indexar(nuc, n.NucleoNombre, n.NucleoId);
            }
            var gal = new Dictionary<string, string>();
            foreach (var ga in galpones.Where(x => x.GranjaId == g.Id))
            {
                Indexar(gal, ga.GalponId, ga.GalponId);
                Indexar(gal, ga.GalponNombre, ga.GalponId);
            }

            var clave = MigracionCalculos.NormalizarClave(g.Name);
            if (string.IsNullOrEmpty(clave)) continue;
            if (!porNombre.TryGetValue(clave, out var lista)) porNombre[clave] = lista = new List<GranjaUbicaciones>();
            lista.Add(new GranjaUbicaciones(g.Id, g.Name, nuc, gal));
        }
        return porNombre;

        // Un nombre de galpón repetido en la granja (existe: "4" en dos galpones de DAYLAND) no puede
        // resolverse a ciegas; se deja solo la primera aparición y el código sigue siendo unívoco.
        static void Indexar(Dictionary<string, string> destino, string? clave, string id)
        {
            var k = MigracionCalculos.NormalizarClave(clave);
            if (string.IsNullOrEmpty(k)) return;
            if (!destino.ContainsKey(k)) destino[k] = id.Trim();
        }
    }

    /// <summary>
    /// ¿La granja maneja el alimento a NIVEL GALPÓN? Flag configurable por empresa/granja
    /// (<see cref="AlimentoNivelResolver"/>). Sin datos de la granja se asume nivel granja, el
    /// comportamiento seguro (no exige galpón), igual que <c>InventarioGestionService</c>.
    /// </summary>
    private async Task<bool> ManejaAlimentoPorGalponAsync(int farmId, CancellationToken ct)
    {
        var flags = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == farmId)
            .Join(_ctx.Set<Company>().AsNoTracking(), f => f.CompanyId, c => c.Id,
                (f, c) => new { Farm = f.ManejaAlimentoPorGalpon, Company = c.ManejaAlimentoPorGalpon })
            .FirstOrDefaultAsync(ct);
        return flags is not null && AlimentoNivelResolver.ManejaPorGalpon(flags.Farm, flags.Company);
    }

    /// <summary>
    /// Lee y valida la hoja "Alimento". Devuelve las filas resueltas a ids; los problemas se acumulan
    /// en <paramref name="errores"/>. Si el archivo no trae la hoja, devuelve lista vacía sin error.
    /// </summary>
    /// <param name="destinoDefault">
    /// Ubicación que toman las filas sin Granja/Núcleo/Galpón: la del lote elegido en pantalla (el
    /// caso normal — "todo este alimento entró acá").
    /// </param>
    /// <param name="manejaPorGalpon">
    /// Nivel efectivo del alimento en esa granja. En nivel GRANJA (Colombia: Sanmarino, Santa Reyes)
    /// núcleo y galpón se descartan con Advertencia: es donde vive el stock y es lo que exige
    /// <c>InventarioGestionService</c>, que LANZA si le mandan ubicación a una granja de nivel granja.
    /// </param>
    private async Task<List<MovimientoAlimentoFila>> LeerHojaAlimentoAsync(
        IFormFile file, int companyId, UbicacionAlimento destinoDefault, bool manejaPorGalpon,
        List<MigracionErrorDto> errores, CancellationToken ct,
        ModoUbicacionInventario modo = ModoUbicacionInventario.Clasico)
    {
        var filas = LeerHojaOpcionalConEsquema(file, MigracionEsquemas.AlimentoEngorde, errores);
        if (filas.Count == 0) return new List<MovimientoAlimentoFila>();

        var (_, alimentosPorClave) = await CargarAlimentosEmpresaAsync(companyId, ct);
        var granjasPorNombre = await CargarUbicacionesEmpresaAsync(companyId, ct);
        // Solo se consulta en modo por silo: las empresas sin el flag no pagan esta query.
        var silosPorGranja = modo == ModoUbicacionInventario.PorSilo
            ? await CargarSilosEmpresaAsync(companyId, ct)
            : new Dictionary<int, Dictionary<string, List<(int Id, string Nombre)>>>();

        destinoDefault = MigracionPosturaCalculos.NormalizarUbicacionSegunNivel(destinoDefault, manejaPorGalpon);

        var resultado = new List<MovimientoAlimentoFila>();
        var clavesVistas = new HashSet<string>();

        foreach (var fila in filas)
        {
            int e0 = errores.Count;

            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Alimento: fecha inválida o faltante.")); continue; }

            var movTxt = MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento("Movimiento")));
            if (!MigracionAlimentoCalculos.TryMovimiento(movTxt, out var movimiento))
            { errores.Add(new(fila.Numero, "Movimiento", movTxt, $"Alimento: '{movTxt}' no es un movimiento válido. Usá Ingreso, Traslado o Recepción.")); continue; }

            var alimentoTxt = MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento("Alimento")));
            if (alimentoTxt is null)
            { errores.Add(new(fila.Numero, "Alimento", null, "Alimento: indicá el alimento (nombre o código de la hoja Referencias).")); continue; }
            if (!alimentosPorClave.TryGetValue(MigracionCalculos.NormalizarClave(alimentoTxt), out var matches) || matches.Count == 0)
            { errores.Add(new(fila.Numero, "Alimento", alimentoTxt, $"El alimento '{alimentoTxt}' no existe en el inventario de la empresa (concepto alimento, activo). Usá el nombre o código de la hoja Referencias.")); continue; }
            if (matches.Count > 1)
            { errores.Add(new(fila.Numero, "Alimento", alimentoTxt, $"'{alimentoTxt}' coincide con {matches.Count} alimentos distintos; usá el código.")); continue; }

            var cantidad = DecimalNoNeg(fila, errores, "Cantidad", ClavesAlimento("Cantidad"));
            if (errores.Count > e0) continue;
            if (cantidad is null or <= 0)
            { errores.Add(new(fila.Numero, "Cantidad", cantidad?.ToString(), "Alimento: la cantidad es obligatoria y debe ser mayor que cero.")); continue; }

            var unidad = LeerUnidadAlimento(fila, errores);
            if (errores.Count > e0) continue;
            var cantidadKg = MigracionCalculos.ConsumoAKilos(cantidad, unidad)!.Value;

            var destino = ResolverUbicacion(fila, errores, granjasPorNombre, destinoDefault,
                "Granja", "Núcleo", "Galpón", esOrigen: false);
            if (errores.Count > e0) continue;
            destino = AjustarUbicacionAlNivel(destino, manejaPorGalpon, fila, errores, "Galpón");
            destino = ResolverSiloDeUbicacion(destino, modo, silosPorGranja, fila, errores, "Silo");
            if (errores.Count > e0) continue;

            // Un Consumo sin referencia no se puede distinguir de otro igual del mismo día: la
            // idempotencia lo tomaría por repetido y la segunda salida no se aplicaría.
            if (movimiento is MovimientoAlimento.Consumo
                && MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento("Referencia"))) is null)
                errores.Add(new(fila.Numero, "Referencia", null,
                    "Alimento: un movimiento 'Consumo' necesita Referencia (de dónde sale ese consumo: lote, día, documento). Sin ella, dos consumos iguales del mismo día se toman por repetidos.", "Advertencia"));

            UbicacionAlimento? origen = null;
            if (movimiento is MovimientoAlimento.Traslado or MovimientoAlimento.Recepcion)
            {
                var tieneOrigen = Celda(fila, ClavesAlimento("Granja Origen")) is not null
                               || Celda(fila, ClavesAlimento("Núcleo Origen")) is not null
                               || Celda(fila, ClavesAlimento("Galpón Origen")) is not null;
                if (!tieneOrigen)
                { errores.Add(new(fila.Numero, "Granja Origen", null, $"Alimento: un movimiento '{movimiento}' necesita la ubicación de origen (Granja/Núcleo/Galpón Origen).")); continue; }

                origen = ResolverUbicacion(fila, errores, granjasPorNombre, destinoDefault,
                    "Granja Origen", "Núcleo Origen", "Galpón Origen", esOrigen: true);
                if (errores.Count > e0) continue;
                origen = AjustarUbicacionAlNivel(origen.Value, manejaPorGalpon, fila, errores, "Galpón Origen");
                origen = ResolverSiloDeUbicacion(origen.Value, modo, silosPorGranja, fila, errores, "Silo Origen");
                if (errores.Count > e0) continue;
            }

            var origenTxt = MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento("Origen")));
            if (!MigracionAlimentoCalculos.TryOrigenIngreso(origenTxt, out var origenTipo))
            { errores.Add(new(fila.Numero, "Origen", origenTxt, $"Alimento: '{origenTxt}' no es un origen válido. Usá planta, bodega o granja.")); continue; }

            var referencia = MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento("Referencia")));

            // Duplicado DENTRO del archivo: dos filas idénticas duplicarían el stock en silencio.
            var clave = MigracionAlimentoCalculos.ClaveIdempotencia(movimiento, destino, matches[0].Id, fecha, cantidadKg, referencia);
            if (!clavesVistas.Add(clave))
            {
                errores.Add(new(fila.Numero, "Cantidad", cantidadKg.ToString("0.###"),
                    $"Alimento: fila repetida ({matches[0].Nombre}, {fecha:yyyy-MM-dd}, {cantidadKg:0.###} kg). Si son dos entradas reales del mismo día, diferenciálas con la columna Referencia."));
                continue;
            }

            resultado.Add(new MovimientoAlimentoFila(
                fila.Numero, movimiento, fecha, matches[0].Id, matches[0].Nombre, cantidadKg,
                destino, origen, origenTipo, referencia,
                MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento("Observaciones")))));
        }

        return resultado;
    }

    /// <summary>
    /// Silos y bodegas de la empresa, indexados por granja y por clave normalizada (nombre Y código
    /// ERP de ubicación). Es el equivalente de <c>CargarUbicacionesEmpresaAsync</c> para las empresas
    /// que ubican el inventario por silo: el usuario escribe el NOMBRE que ve en pantalla y acá se
    /// traduce al id que espera el servicio de inventario.
    /// </summary>
    private async Task<Dictionary<int, Dictionary<string, List<(int Id, string Nombre)>>>> CargarSilosEmpresaAsync(
        int companyId, CancellationToken ct)
    {
        var silos = await _ctx.FarmSilos.AsNoTracking()
            .Where(fs => fs.CompanyId == companyId && fs.DeletedAt == null && fs.Activo)
            .Select(fs => new { fs.Id, fs.GranjaId, fs.Nombre, fs.CodigoErpUbicacion })
            .ToListAsync(ct);

        var porGranja = new Dictionary<int, Dictionary<string, List<(int, string)>>>();
        foreach (var silo in silos)
        {
            if (!porGranja.TryGetValue(silo.GranjaId, out var porClave))
                porGranja[silo.GranjaId] = porClave = new Dictionary<string, List<(int, string)>>();

            Indexar(porClave, silo.Nombre, (silo.Id, silo.Nombre));
            Indexar(porClave, silo.CodigoErpUbicacion, (silo.Id, silo.Nombre));
        }
        return porGranja;

        static void Indexar(Dictionary<string, List<(int, string)>> destino, string? texto, (int, string) valor)
        {
            var clave = MigracionCalculos.NormalizarClave(texto);
            if (string.IsNullOrEmpty(clave)) return;
            if (!destino.TryGetValue(clave, out var lista)) destino[clave] = lista = new List<(int, string)>();
            if (!lista.Contains(valor)) lista.Add(valor);
        }
    }

    /// <summary>
    /// Resuelve la columna de silo de una fila y la mete en la ubicación, aplicando la MISMA regla que
    /// el alta manual (<see cref="InventarioUbicacionSiloCalculos.ValidarUbicacion"/> +
    /// <see cref="UbicacionAlimento.ParaModo"/>): en modo por silo es obligatorio y anula
    /// núcleo/galpón; en modo clásico se rechaza si viene.
    ///
    /// <para>
    /// El silo se busca entre los de la granja de ESA ubicación, no entre los de la empresa: un
    /// nombre como «Silo 4» existe en varias granjas y aceptarlo suelto movería el stock de otra.
    /// </para>
    /// </summary>
    private static UbicacionAlimento ResolverSiloDeUbicacion(
        UbicacionAlimento ubicacion, ModoUbicacionInventario modo,
        IReadOnlyDictionary<int, Dictionary<string, List<(int Id, string Nombre)>>> silosPorGranja,
        FilaCruda fila, List<MigracionErrorDto> errores, string columna)
    {
        var texto = MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento(columna)));

        if (modo == ModoUbicacionInventario.Clasico)
        {
            if (texto is not null)
                errores.Add(new(fila.Numero, columna, texto, $"Alimento: {InventarioUbicacionSiloCalculos.MensajeSiloNoAplica}"));
            return ubicacion.ParaModo(modo);
        }

        if (texto is null)
        {
            errores.Add(new(fila.Numero, columna, null,
                $"Alimento: {InventarioUbicacionSiloCalculos.MensajeSiloRequerido} Elegí uno de la hoja Referencias."));
            return ubicacion;
        }

        if (!silosPorGranja.TryGetValue(ubicacion.FarmId, out var porClave)
            || !porClave.TryGetValue(MigracionCalculos.NormalizarClave(texto), out var matches)
            || matches.Count == 0)
        {
            errores.Add(new(fila.Numero, columna, texto,
                $"El silo o bodega '{texto}' no existe en esa granja (activo). Usá el nombre o el código de la hoja Referencias."));
            return ubicacion;
        }
        if (matches.Count > 1)
        {
            errores.Add(new(fila.Numero, columna, texto,
                $"'{texto}' coincide con {matches.Count} silos de esa granja; usá el código ERP."));
            return ubicacion;
        }

        return (ubicacion with { SiloId = matches[0].Id }).ParaModo(modo);
    }

    /// <summary>
    /// Ajusta una ubicación de la hoja al NIVEL efectivo de la granja. En nivel granja el núcleo y el
    /// galpón se descartan y se avisa (Advertencia, no error): el movimiento se aplica igual, pero el
    /// usuario tiene que saber que su columna no tuvo efecto. Propagarlos haría que
    /// <c>RegistrarIngresoAsync</c> lance y la fila se perdiera con un mensaje de infraestructura.
    /// </summary>
    private static UbicacionAlimento AjustarUbicacionAlNivel(
        UbicacionAlimento ubicacion, bool manejaPorGalpon, FilaCruda fila,
        List<MigracionErrorDto> errores, string columna)
    {
        if (!MigracionPosturaCalculos.UbicacionTraeDetalleIgnorado(ubicacion, manejaPorGalpon))
            return MigracionPosturaCalculos.NormalizarUbicacionSegunNivel(ubicacion, manejaPorGalpon);

        var n = ubicacion.Normalizada();
        errores.Add(new(fila.Numero, columna, n.GalponId ?? n.NucleoId,
            "Alimento: esta granja maneja el alimento a NIVEL GRANJA; se ignoran Núcleo/Galpón y el movimiento se registra sobre la granja (es donde vive el stock que consume el seguimiento).",
            "Advertencia"));
        return MigracionPosturaCalculos.NormalizarUbicacionSegunNivel(ubicacion, manejaPorGalpon);
    }

    /// <summary>Claves de lectura (título + alias) de una columna de la hoja "Alimento".</summary>
    private static string[] ClavesAlimento(string titulo) =>
        MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.AlimentoEngorde, titulo);

    /// <summary>Unidad de la hoja "Alimento": kg (default) o qq. Espeja <c>LeerUnidadConsumo</c>.</summary>
    private static string LeerUnidadAlimento(FilaCruda fila, List<MigracionErrorDto> errores)
    {
        var txt = MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento("Unidad")));
        if (txt is null) return "kg";
        var k = MigracionCalculos.NormalizarClave(txt);
        if (k is "kg" or "kilos" or "kilogramos") return "kg";
        if (k is "qq" or "quintal" or "quintales") return "qq";
        errores.Add(new(fila.Numero, "Unidad", txt, "Alimento: la unidad debe ser 'kg' o 'qq'."));
        return "kg";
    }

    /// <summary>
    /// Resuelve un trío Granja/Núcleo/Galpón de la hoja. Los tres vacíos ⇒ la ubicación del lote
    /// seleccionado en pantalla. Con granja indicada, núcleo y galpón se buscan DENTRO de esa granja
    /// (por código o por nombre).
    /// </summary>
    private static UbicacionAlimento ResolverUbicacion(
        FilaCruda fila, List<MigracionErrorDto> errores,
        Dictionary<string, List<GranjaUbicaciones>> granjasPorNombre,
        UbicacionAlimento porDefecto,
        string colGranja, string colNucleo, string colGalpon, bool esOrigen)
    {
        var granjaTxt = MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento(colGranja)));
        var nucleoTxt = MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento(colNucleo)));
        var galponTxt = MigracionCalculos.TextoLimpio(Celda(fila, ClavesAlimento(colGalpon)));

        if (granjaTxt is null && nucleoTxt is null && galponTxt is null)
            return porDefecto;

        GranjaUbicaciones? granja = null;
        if (granjaTxt is null)
        {
            // Sin granja pero con núcleo/galpón: se asume la granja del lote y se validan ahí.
            granja = granjasPorNombre.Values.SelectMany(x => x).FirstOrDefault(g => g.FarmId == porDefecto.FarmId);
            if (granja is null)
            { errores.Add(new(fila.Numero, colGranja, null, $"Alimento: indicá también {colGranja} para ubicar el núcleo/galpón.")); return porDefecto; }
        }
        else
        {
            var candidatas = granjasPorNombre.TryGetValue(MigracionCalculos.NormalizarClave(granjaTxt), out var lista)
                ? lista : new List<GranjaUbicaciones>();
            if (candidatas.Count == 0)
            { errores.Add(new(fila.Numero, colGranja, granjaTxt, $"Alimento: no existe una granja '{granjaTxt}' en la empresa.")); return porDefecto; }
            if (candidatas.Count > 1)
            { errores.Add(new(fila.Numero, colGranja, granjaTxt, $"Alimento: el nombre de granja '{granjaTxt}' está repetido ({candidatas.Count} coincidencias); no se puede resolver.")); return porDefecto; }
            granja = candidatas[0];
        }

        string? nucleoId = null, galponId = null;
        if (nucleoTxt is not null)
        {
            if (!granja.NucleosPorClave.TryGetValue(MigracionCalculos.NormalizarClave(nucleoTxt), out var nid))
            { errores.Add(new(fila.Numero, colNucleo, nucleoTxt, $"Alimento: el núcleo '{nucleoTxt}' no pertenece a la granja {granja.Nombre}.")); return porDefecto; }
            nucleoId = nid;
        }
        if (galponTxt is not null)
        {
            if (!granja.GalponesPorClave.TryGetValue(MigracionCalculos.NormalizarClave(galponTxt), out var gid))
            { errores.Add(new(fila.Numero, colGalpon, galponTxt, $"Alimento: el galpón '{galponTxt}' no pertenece a la granja {granja.Nombre}.")); return porDefecto; }
            galponId = gid;
        }

        // Núcleo omitido pero galpón indicado: el núcleo sale del propio galpón (la granja lo sabe).
        if (nucleoId is null && galponId is not null && granja.FarmId == porDefecto.FarmId)
            nucleoId = porDefecto.NucleoId;

        _ = esOrigen; // el trío se valida igual sea origen o destino; el parámetro documenta la intención.
        return new UbicacionAlimento(granja.FarmId, nucleoId, galponId).Normalizada();
    }

    /// <summary>Nombre del alimento para los mensajes del reporte (cae al id si el ítem ya no existe).</summary>
    private async Task<string> NombreAlimentoAsync(int itemId, CancellationToken ct) =>
        await _ctx.ItemInventario.AsNoTracking()
            .Where(i => i.Id == itemId).Select(i => i.Nombre).FirstOrDefaultAsync(ct)
        ?? $"Ítem #{itemId}";

    /// <summary>Granja del lote de engorde (para heredar la ubicación por defecto de los movimientos).</summary>
    private async Task<int> GranjaIdDeLoteAsync(int loteId, CancellationToken ct) =>
        await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId)
            .Select(l => l.GranjaId)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Aplica los movimientos de la hoja "Alimento" delegando en IInventarioGestionService. Se ejecuta
    /// ANTES de insertar el seguimiento, para que el consumo del día encuentre stock. Devuelve cuántos
    /// se aplicaron y cuántos se omitieron por ya existir (idempotencia).
    /// </summary>
    private async Task<(int Aplicados, int Omitidos)> AplicarMovimientosAlimentoAsync(
        List<MovimientoAlimentoFila> movimientos, List<MigracionErrorDto> fallos, CancellationToken ct)
    {
        if (movimientos.Count == 0 || _inventarioGestion is null) return (0, 0);

        var yaAplicados = await ClavesMovimientosExistentesAsync(movimientos, ct);
        int aplicados = 0, omitidos = 0;

        // El nivel se resuelve por GRANJA (un traslado puede cruzar dos granjas con niveles distintos)
        // y se cachea: son N filas contra un puñado de granjas.
        var nivelPorGranja = new Dictionary<int, bool>();
        async Task<bool> PorGalponAsync(int farmId)
        {
            if (nivelPorGranja.TryGetValue(farmId, out var v)) return v;
            v = await ManejaAlimentoPorGalponAsync(farmId, ct);
            nivelPorGranja[farmId] = v;
            return v;
        }

        // En orden de fecha: el histórico queda cronológico y una recepción nunca precede a su traslado.
        foreach (var m in movimientos.OrderBy(x => x.Fecha).ThenBy(x => x.Numero))
        {
            var clave = MigracionAlimentoCalculos.ClaveIdempotencia(m.Movimiento, m.Destino, m.ItemId, m.Fecha, m.CantidadKg, m.Referencia);
            if (yaAplicados.Contains(clave)) { omitidos++; continue; }

            try
            {
                switch (m.Movimiento)
                {
                    case MovimientoAlimento.Ingreso:
                        await _inventarioGestion.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                            m.Destino.FarmId, m.Destino.NucleoId, m.Destino.GalponId, m.ItemId, m.CantidadKg, "kg",
                            m.Referencia, m.Observaciones ?? "Carga masiva de seguimiento engorde",
                            OrigenTipo: m.OrigenTipo,
                            OrigenFarmId: m.OrigenTipo is "granja" or "bodega" ? m.Origen?.FarmId : null,
                            FechaMovimiento: m.Fecha,
                            SiloId: m.Destino.SiloId), ct);
                        break;

                    case MovimientoAlimento.Traslado:
                        var o = m.Origen!.Value;
                        await _inventarioGestion.RegistrarTrasladoAsync(new InventarioGestionTrasladoRequest(
                            o.FarmId, o.NucleoId, o.GalponId,
                            m.Destino.FarmId, m.Destino.NucleoId, m.Destino.GalponId,
                            m.ItemId, m.CantidadKg, "kg", m.Referencia,
                            m.Observaciones ?? "Carga masiva de seguimiento engorde",
                            DestinoTipo: "granja", FechaMovimiento: m.Fecha,
                            FromSiloId: o.SiloId, ToSiloId: m.Destino.SiloId), ct);
                        break;

                    case MovimientoAlimento.Recepcion:
                        await RecibirTransitoAlimentoAsync(m, ct);
                        break;

                    case MovimientoAlimento.Consumo:
                        var pedido = new InventarioGestionConsumoRequest(
                            m.Destino.FarmId, m.Destino.NucleoId, m.Destino.GalponId, m.ItemId, m.CantidadKg, "kg",
                            m.Referencia, m.Observaciones ?? "Carga masiva de seguimiento engorde",
                            FechaMovimiento: m.Fecha,
                            SiloId: m.Destino.SiloId);
                        // RegistrarConsumoAsync EXIGE núcleo+galpón para alimento, sin mirar el flag: en
                        // una granja de nivel granja hay que ir por la variante dedicada (la misma que
                        // usa ColombiaInventarioConsumoService).
                        //
                        // F4 (22-ago-2026): RegistrarConsumoNivelGranjaAsync AHORA commitea sola (abre su
                        // propia EnTransaccionAsync si no hay una ambiente) — antes dependía del
                        // SaveChangesAsync de abajo. Se revisó explícitamente si esto rompe el "todo o
                        // nada" del bucle y la respuesta es que NO HABÍA tal garantía: ni este archivo ni
                        // sus dos llamadores (MigracionService.Historicos.cs, .SeguimientoEngorde.cs)
                        // abren una transacción ambiente (grep verificado, cero resultados), así que cada
                        // iteración YA persistía su SaveChangesAsync por separado — el catch de abajo
                        // reporta el fallo y sigue con el siguiente movimiento a propósito («a diferencia
                        // del descuento del seguimiento, acá el fallo SE REPORTA»); envolver el bucle
                        // entero en una transacción cambiaría ese comportamiento deliberado de reporte
                        // parcial a todo-o-nada, que no es lo que este importador hace hoy. El
                        // SaveChangesAsync que sigue queda como no-op inofensivo (nada pendiente).
                        // Con silo, el stock vive a nivel granja (núcleo/galpón en NULL por decisión de
                        // la Fase B), así que va por la variante de nivel granja aunque la granja
                        // maneje alimento por galpón: RegistrarConsumoAsync exige núcleo+galpón.
                        //
                        // ⚠️ `RegistrarConsumoNivelGranjaAsync` es FAIL-OPEN con el silo: no llama
                        // `ValidarSiloDeGranjaAsync` ni `ValidarUbicacion`, manda `req.SiloId` tal cual
                        // al stock atómico. Los tres agujeros que eso deja (silo de OTRA granja, silo
                        // inactivo, silo en una empresa sin el flag) los cierra el PARSEO:
                        // `ResolverSiloDeUbicacion` resuelve el nombre SOLO entre los silos activos de
                        // la granja de esa misma ubicación, y en modo clásico rechaza cualquier silo.
                        if (m.Destino.SiloId is null && await PorGalponAsync(m.Destino.FarmId))
                            await _inventarioGestion.RegistrarConsumoAsync(pedido, ct);
                        else
                        {
                            await _inventarioGestion.RegistrarConsumoNivelGranjaAsync(pedido, ct);
                            await _ctx.SaveChangesAsync(ct);
                        }
                        break;
                }
                aplicados++;
            }
            catch (Exception ex)
            {
                // A diferencia del descuento del seguimiento (que loguea y sigue), acá el fallo se
                // REPORTA: un movimiento de inventario perdido es justo lo que descuadra el galpón.
                fallos.Add(new(m.Numero, "Alimento", m.ItemNombre,
                    $"No se pudo registrar el {m.Movimiento.ToString().ToLowerInvariant()} de {m.CantidadKg:0.###} kg de {m.ItemNombre} ({m.Fecha:yyyy-MM-dd}): {ex.Message}"));
            }
        }

        return (aplicados, omitidos);
    }

    /// <summary>
    /// Recepción: busca el tránsito pendiente que coincide con la fila (granja destino + ítem) y lo
    /// acepta. Sin tránsito coincidente es un error de datos, no una excepción de infraestructura.
    /// </summary>
    private async Task RecibirTransitoAlimentoAsync(MovimientoAlimentoFila m, CancellationToken ct)
    {
        var pendientes = await _inventarioGestion!.GetTransitosPendientesAsync(m.Destino.FarmId, ct);
        var candidato = pendientes
            .Where(p => p.ItemInventarioEcuadorId == m.ItemId && p.Quantity == m.CantidadKg)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefault()
            ?? pendientes.Where(p => p.ItemInventarioEcuadorId == m.ItemId).OrderBy(p => p.CreatedAt).FirstOrDefault();

        if (candidato is null)
            throw new InvalidOperationException(
                $"no hay un traslado en tránsito pendiente de {m.ItemNombre} hacia esa granja. Cargá primero el Traslado que lo origina.");

        await _inventarioGestion.RegistrarRecepcionTransitoAsync(new InventarioGestionRecepcionTransitoRequest(
            candidato.TransferGroupId, m.Destino.FarmId, m.Destino.NucleoId, m.Destino.GalponId), ct);
    }

    /// <summary>
    /// Claves de idempotencia de los movimientos que YA están en el histórico, para no duplicar stock
    /// si el mismo archivo se importa dos veces. Se consulta solo el rango de fechas y las ubicaciones
    /// del archivo.
    /// </summary>
    private async Task<HashSet<string>> ClavesMovimientosExistentesAsync(List<MovimientoAlimentoFila> movimientos, CancellationToken ct)
    {
        // Origen incluido: el traslado inter-granja se registra sobre la granja ORIGEN (con el
        // destino en From*), así que filtrar solo por destinos dejaba esos movimientos invisibles.
        var farmIds = movimientos
            .SelectMany(m => new[] { m.Destino.FarmId, m.Origen?.FarmId ?? m.Destino.FarmId })
            .Distinct().ToList();
        var itemIds = movimientos.Select(m => m.ItemId).Distinct().ToList();
        var desde = movimientos.Min(m => m.Fecha).Date.AddDays(-1);
        var hasta = movimientos.Max(m => m.Fecha).Date.AddDays(1);
        var desdeUtc = new DateTimeOffset(desde, TimeSpan.Zero);
        var hastaUtc = new DateTimeOffset(hasta, TimeSpan.Zero);

        var existentes = await _ctx.InventarioGestionMovimientos.AsNoTracking()
            .Where(x => farmIds.Contains(x.FarmId)
                        && itemIds.Contains(x.ItemInventarioEcuadorId)
                        && x.CreatedAt >= desdeUtc && x.CreatedAt <= hastaUtc)
            .Select(x => new { x.FarmId, x.NucleoId, x.GalponId, x.FromFarmId, x.FromNucleoId, x.FromGalponId,
                               x.SiloId, x.FromSiloId,
                               x.ItemInventarioEcuadorId, x.Quantity, x.Reference, x.CreatedAt, x.MovementType })
            .ToListAsync(ct);

        var claves = new HashSet<string>();
        foreach (var e in existentes)
        {
            // La clave del archivo se arma con el DESTINO de la fila: cada movement_type real del
            // servicio se reconstruye hacia esa misma ubicación. (El literal "Traslado" nunca se
            // emite — mapearlo dejaba los traslados aplicados invisibles y un reimport tras un
            // fallo parcial los volvía a contar en el balance.)
            MovimientoAlimento? mov;
            var ubicacion = new UbicacionAlimento(e.FarmId, e.NucleoId, e.GalponId, e.SiloId);
            switch (e.MovementType)
            {
                case "Ingreso":
                    mov = MovimientoAlimento.Ingreso; break;
                // Traslado misma granja: dos patas; la de ENTRADA lleva la ubicación destino.
                case "TrasladoEntrada":
                    mov = MovimientoAlimento.Traslado; break;
                // Traslado inter-granja: UNA fila (salida en tránsito) sobre el ORIGEN; el destino
                // viaja en From* (semántica invertida del tránsito).
                case "TrasladoInterGranjaSalida":
                    mov = MovimientoAlimento.Traslado;
                    ubicacion = new UbicacionAlimento(e.FromFarmId ?? e.FarmId, e.FromNucleoId, e.FromGalponId, e.FromSiloId);
                    break;
                // Recepción de tránsito ya aceptada (fila sobre el destino elegido al recibir).
                case "TrasladoInterGranjaEntrada":
                    mov = MovimientoAlimento.Recepcion; break;
                // Solo el consumo con REFERENCIA propia entra en la idempotencia de la hoja: los
                // consumos que genera el seguimiento diario llevan "Seguimiento aves engorde #…" y no
                // deben tapar una fila de Consumo del archivo.
                case "Consumo" when !(e.Reference ?? "").StartsWith("Seguimiento ", StringComparison.OrdinalIgnoreCase):
                    mov = MovimientoAlimento.Consumo; break;
                default:
                    mov = null; break;
            }
            if (mov is null) continue;
            claves.Add(MigracionAlimentoCalculos.ClaveIdempotencia(
                mov.Value,
                ubicacion,
                e.ItemInventarioEcuadorId,
                e.CreatedAt.UtcDateTime.Date,
                e.Quantity,
                e.Reference));
        }
        return claves;
    }

    /// <summary>
    /// Stock actual de las posiciones que toca el archivo, para la simulación del balance.
    /// </summary>
    private async Task<Dictionary<PosicionAlimento, decimal>> CargarStockPosicionesAsync(
        IReadOnlyCollection<PosicionAlimento> posiciones, CancellationToken ct)
    {
        var mapa = new Dictionary<PosicionAlimento, decimal>();
        if (posiciones.Count == 0) return mapa;

        var farmIds = posiciones.Select(p => p.Ubicacion.FarmId).Distinct().ToList();
        var itemIds = posiciones.Select(p => p.ItemId).Distinct().ToList();

        var filas = await _ctx.InventarioGestionStock.AsNoTracking()
            .Where(s => farmIds.Contains(s.FarmId) && itemIds.Contains(s.ItemInventarioEcuadorId))
            .Select(s => new { s.FarmId, s.NucleoId, s.GalponId, s.SiloId, s.ItemInventarioEcuadorId, s.Quantity })
            .ToListAsync(ct);

        foreach (var f in filas)
        {
            // El silo entra en la clave: sin él, una empresa con 39 silos veía TODO su stock sumado
            // en una sola posición y el dry-run aprobaba archivos que después no se podían aplicar.
            var pos = new PosicionAlimento(
                new UbicacionAlimento(f.FarmId, f.NucleoId, f.GalponId, f.SiloId).Normalizada(), f.ItemInventarioEcuadorId);
            mapa[pos] = mapa.TryGetValue(pos, out var v) ? v + f.Quantity : f.Quantity;
        }
        return mapa;
    }
}
