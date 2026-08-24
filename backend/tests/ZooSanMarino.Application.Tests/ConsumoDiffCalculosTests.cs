using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del diff de ítems al EDITAR un seguimiento diario (F1 del plan
/// `descuento_inventario_movil_plan.md`).
///
/// <para>
/// Estos tests son la razón de ser de <see cref="ConsumoDiffCalculos"/>: el mismo bucle está escrito
/// inline en tres services de Infrastructure y <c>ZooSanMarino.Application.Tests</c> no referencia
/// Infrastructure, así que hasta ahora no había manera de cubrirlo. Lo que se fija acá es el
/// comportamiento que las tres copias comparten, más el orden determinista que ninguna tiene.
/// </para>
/// </summary>
public class ConsumoDiffCalculosTests
{
    // Atajos legibles: catálogo (modelo A) vs inventario unificado, con y sin silo.
    private static ItemConsumoKey Cat(int id, int? silo = null) => new(id, false, silo);
    private static ItemConsumoKey Inv(int id, int? silo = null) => new(id, true, silo);

    private static Dictionary<ItemConsumoKey, decimal> Mapa(params (ItemConsumoKey Clave, decimal Kg)[] filas)
    {
        var d = new Dictionary<ItemConsumoKey, decimal>();
        foreach (var (clave, kg) in filas) d[clave] = kg;
        return d;
    }

    private static readonly Dictionary<ItemConsumoKey, decimal> Vacio = new();

    // ── El ítem APARECE en la edición ───────────────────────────────────────────────────────

    [Fact]
    public void ItemNuevo_EsIncrementoPorElTotal_YMovimientoDeConsumo()
    {
        var viejos = Vacio;
        var nuevos = Mapa((Inv(150), 320m));

        var incrementos = ConsumoDiffCalculos.Incrementos(viejos, nuevos);
        var movimientos = ConsumoDiffCalculos.Movimientos(viejos, nuevos);

        Assert.Equal(320m, incrementos[Inv(150)]);
        Assert.Single(incrementos);

        var mov = Assert.Single(movimientos);
        Assert.Equal(Inv(150), mov.Clave);
        Assert.True(mov.EsConsumo);
        Assert.Equal(320m, mov.Cantidad);
    }

    // ── El ítem DESAPARECE de la edición ────────────────────────────────────────────────────

    [Fact]
    public void ItemQueDesaparece_NoEsIncremento_YDevuelveTodoLoQueTenia()
    {
        // Borrar la línea del formulario tiene que reponer el stock completo. Es el caso que hace
        // que corregir un seguimiento cargado en el ítem equivocado no deje kilos descontados de más.
        var viejos = Mapa((Inv(150), 320m));
        var nuevos = Vacio;

        var incrementos = ConsumoDiffCalculos.Incrementos(viejos, nuevos);
        var movimientos = ConsumoDiffCalculos.Movimientos(viejos, nuevos);

        Assert.Empty(incrementos);

        var mov = Assert.Single(movimientos);
        Assert.Equal(Inv(150), mov.Clave);
        Assert.True(mov.EsDevolucion);
        Assert.Equal(320m, mov.Cantidad);
        Assert.Equal(-320m, mov.Diff);
    }

    // ── Cambia la CANTIDAD ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CantidadQueSube_SoloElDeltaEsIncremento_NoElTotalNuevo()
    {
        // Validar el total nuevo (500) y no el delta (180) rechazaría la edición por falta de stock
        // aunque los 320 originales ya estén descontados.
        var viejos = Mapa((Inv(150), 320m));
        var nuevos = Mapa((Inv(150), 500m));

        var incrementos = ConsumoDiffCalculos.Incrementos(viejos, nuevos);
        var mov = Assert.Single(ConsumoDiffCalculos.Movimientos(viejos, nuevos));

        Assert.Equal(180m, incrementos[Inv(150)]);
        Assert.Single(incrementos);
        Assert.True(mov.EsConsumo);
        Assert.Equal(180m, mov.Cantidad);
    }

    [Fact]
    public void CantidadQueBaja_NoEsIncremento_YDevuelveLaDiferencia()
    {
        // Una edición a la baja devuelve, y devolver nunca puede fallar por falta de stock: por eso
        // los negativos quedan fuera de la validación previa.
        var viejos = Mapa((Inv(150), 500m));
        var nuevos = Mapa((Inv(150), 320m));

        var incrementos = ConsumoDiffCalculos.Incrementos(viejos, nuevos);
        var mov = Assert.Single(ConsumoDiffCalculos.Movimientos(viejos, nuevos));

        Assert.Empty(incrementos);
        Assert.True(mov.EsDevolucion);
        Assert.Equal(180m, mov.Cantidad);
    }

    // ── El ítem NO cambia ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ItemSinCambios_NoGeneraIncrementoNiMovimiento()
    {
        // Caso mayoritario de una edición real (se corrige la mortalidad y el alimento queda igual).
        // Emitir un movimiento de 0 ensuciaría el kardex y revalidaría stock ya descontado.
        var viejos = Mapa((Inv(150), 320m), (Cat(89), 40m));
        var nuevos = Mapa((Inv(150), 320m), (Cat(89), 40m));

        Assert.Empty(ConsumoDiffCalculos.Incrementos(viejos, nuevos));
        Assert.Empty(ConsumoDiffCalculos.Movimientos(viejos, nuevos));
    }

    [Fact]
    public void MapasVacios_NoGeneranNada()
    {
        // Levante y engorde saltean el bloque entero cuando no hay metadata ni ítems previos; acá la
        // equivalencia se ve sin la guarda: sin claves no hay nada que emitir.
        Assert.Empty(ConsumoDiffCalculos.Incrementos(Vacio, Vacio));
        Assert.Empty(ConsumoDiffCalculos.Movimientos(Vacio, Vacio));
        Assert.Empty(ConsumoDiffCalculos.ClavesOrdenadas(Vacio, Vacio));
    }

    // ── Cantidad en CERO ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ItemNuevoEnCero_EsIndistinguibleDeNoEnviarlo()
    {
        // Un ítem elegido en el formulario y dejado en 0 no descuenta nada. El `> 0` estricto de los
        // originales lo trata igual que si no existiera.
        var nuevos = Mapa((Inv(150), 0m));

        Assert.Empty(ConsumoDiffCalculos.Incrementos(Vacio, nuevos));
        Assert.Empty(ConsumoDiffCalculos.Movimientos(Vacio, nuevos));
        // La clave sí está en la unión: no se filtra por cantidad, se filtra por diff.
        Assert.Equal(new[] { Inv(150) }, ConsumoDiffCalculos.ClavesOrdenadas(Vacio, nuevos));
    }

    [Fact]
    public void EditarACero_DevuelveTodo_IgualQueBorrarLaLinea()
    {
        // Poner 0 y borrar la línea tienen que dar el mismo movimiento; si no, el stock dependería de
        // cómo el usuario decidió deshacer la carga.
        var viejos = Mapa((Inv(150), 320m));

        var porCero = ConsumoDiffCalculos.Movimientos(viejos, Mapa((Inv(150), 0m)));
        var porBorrado = ConsumoDiffCalculos.Movimientos(viejos, Vacio);

        Assert.Equal(porBorrado, porCero);
        Assert.Equal(-320m, Assert.Single(porCero).Diff);
    }

    // ── La clave conserva el ORIGEN y el SILO ───────────────────────────────────────────────

    [Fact]
    public void MismoIdEnDosSilos_SonDosClavesYNoSeColapsan()
    {
        // El mismo alimento cargado desde dos silos son dos consumos distintos: cada uno descuenta su
        // propia fila de inventario_gestion_stock. Sumarlos descontaría todo del primero.
        var viejos = Mapa((Inv(150, 7), 100m), (Inv(150, 9), 100m));
        var nuevos = Mapa((Inv(150, 7), 130m), (Inv(150, 9), 60m));

        var incrementos = ConsumoDiffCalculos.Incrementos(viejos, nuevos);
        var movimientos = ConsumoDiffCalculos.Movimientos(viejos, nuevos);

        Assert.Equal(30m, incrementos[Inv(150, 7)]);
        Assert.Single(incrementos);

        Assert.Equal(2, movimientos.Count);
        Assert.Equal(new MovimientoConsumo(Inv(150, 7), 30m), movimientos[0]);
        Assert.Equal(new MovimientoConsumo(Inv(150, 9), -40m), movimientos[1]);
    }

    [Fact]
    public void MismoIdDistintaTablaDeOrigen_SonDosClavesYNoSeColapsan()
    {
        // Los rangos de catalogo_items e item_inventario_ecuador se solapan. La rama Ecuador/Panamá de
        // los originales usa Dictionary<int, decimal> y aplana esto en una sola clave; la versión
        // tipada —la que el plan manda conservar— no.
        var viejos = Mapa((Cat(150), 100m), (Inv(150), 100m));
        var nuevos = Mapa((Cat(150), 100m), (Inv(150), 250m));

        var incrementos = ConsumoDiffCalculos.Incrementos(viejos, nuevos);

        Assert.Equal(150m, incrementos[Inv(150)]);
        Assert.False(incrementos.ContainsKey(Cat(150)));
        Assert.Single(incrementos);
    }

    [Fact]
    public void SiloNuloYSiloConValor_SonClavesDistintas()
    {
        // Una edición que agrega el silo a un ítem que no lo tenía NO es «el mismo ítem»: devuelve el
        // consumo sin ubicar y descuenta uno nuevo en el silo. Es lo que hace que encender el flag de
        // silos no mezcle saldos.
        var viejos = Mapa((Inv(150), 80m));
        var nuevos = Mapa((Inv(150, 7), 80m));

        var movimientos = ConsumoDiffCalculos.Movimientos(viejos, nuevos);

        Assert.Equal(2, movimientos.Count);
        Assert.Equal(new MovimientoConsumo(Inv(150), -80m), movimientos[0]);   // null va primero
        Assert.Equal(new MovimientoConsumo(Inv(150, 7), 80m), movimientos[1]);
    }

    // ── ORDEN de salida: estable y determinista ─────────────────────────────────────────────

    [Fact]
    public void OrdenCanonico_EsPorIdLuegoOrigenLuegoSilo_ConSiloNuloPrimero()
    {
        var nuevos = Mapa(
            (Inv(150, 9), 1m),
            (Cat(89), 1m),
            (Inv(150), 1m),
            (Inv(150, 7), 1m),
            (Cat(150), 1m),
            (Inv(89), 1m));

        var claves = ConsumoDiffCalculos.ClavesOrdenadas(Vacio, nuevos);

        Assert.Equal(
            new[] { Cat(89), Inv(89), Cat(150), Inv(150), Inv(150, 7), Inv(150, 9) },
            claves);
    }

    [Fact]
    public void ElOrdenNoDependeDelOrdenDeInsercionDeLosMapas()
    {
        // Este es el test que justifica la clase: los originales recorren un HashSet, cuyo orden de
        // iteración no está garantizado. Como el orden del recorrido es el orden en que nacen las
        // filas de inventario_gestion_movimiento —y la tabla diaria de engorde desempata intra-día por
        // created_at—, un orden arbitrario ahí produce días que cierran en rojo con el total perfecto.
        var viejos = Mapa((Inv(150), 10m), (Cat(89), 10m), (Inv(300, 4), 10m), (Cat(12), 10m));
        var nuevos = Mapa((Cat(12), 99m), (Inv(300, 4), 1m), (Cat(89), 50m), (Inv(150), 5m));

        var viejosAlReves = Mapa((Cat(12), 10m), (Inv(300, 4), 10m), (Cat(89), 10m), (Inv(150), 10m));
        var nuevosAlReves = Mapa((Inv(150), 5m), (Cat(89), 50m), (Inv(300, 4), 1m), (Cat(12), 99m));

        var esperado = new[]
        {
            new MovimientoConsumo(Cat(12), 89m),
            new MovimientoConsumo(Cat(89), 40m),
            new MovimientoConsumo(Inv(150), -5m),
            new MovimientoConsumo(Inv(300, 4), -9m),
        };

        Assert.Equal(esperado, ConsumoDiffCalculos.Movimientos(viejos, nuevos));
        Assert.Equal(esperado, ConsumoDiffCalculos.Movimientos(viejosAlReves, nuevosAlReves));
    }

    [Fact]
    public void LosIncrementosSeInsertanEnElMismoOrdenCanonico()
    {
        // El mapa de incrementos alimenta ValidarStockConsumoAsync, que recorre y nombra el primer
        // ítem sin stock. Sin orden fijo, dos ejecuciones idénticas pueden culpar a ítems distintos y
        // el usuario recibe mensajes que se contradicen.
        var nuevos = Mapa((Inv(300), 5m), (Cat(12), 5m), (Inv(150, 7), 5m), (Cat(150), 5m));
        var nuevosAlReves = Mapa((Cat(150), 5m), (Inv(150, 7), 5m), (Cat(12), 5m), (Inv(300), 5m));

        var esperado = new[] { Cat(12), Cat(150), Inv(150, 7), Inv(300) };

        Assert.Equal(esperado, ConsumoDiffCalculos.Incrementos(Vacio, nuevos).Keys.ToArray());
        Assert.Equal(esperado, ConsumoDiffCalculos.Incrementos(Vacio, nuevosAlReves).Keys.ToArray());
    }

    // ── Equivalencia con el bucle original ──────────────────────────────────────────────────

    [Fact]
    public void MismoConjuntoDeResultadosQueElBucleInlineOriginal()
    {
        // Refactor = mover código sin cambiar resultados. Lo único que esta clase agrega es el ORDEN;
        // el CONJUNTO de incrementos y de movimientos tiene que ser exactamente el de las tres copias.
        var viejos = Mapa((Inv(150), 320m), (Cat(89), 40m), (Inv(7, 3), 12m));
        var nuevos = Mapa((Inv(150), 500m), (Cat(89), 40m), (Cat(99), 8m));

        // Réplica literal del bucle de Infrastructure (HashSet + GetValueOrDefault).
        var incrementosOriginal = new Dictionary<ItemConsumoKey, decimal>();
        var movimientosOriginal = new List<MovimientoConsumo>();
        var union = new HashSet<ItemConsumoKey>(viejos.Keys);
        foreach (var k in nuevos.Keys) union.Add(k);
        foreach (var key in union)
        {
            var diff = nuevos.GetValueOrDefault(key) - viejos.GetValueOrDefault(key);
            if (diff > 0) incrementosOriginal[key] = diff;
            if (diff != 0) movimientosOriginal.Add(new MovimientoConsumo(key, diff));
        }

        var incrementos = ConsumoDiffCalculos.Incrementos(viejos, nuevos);
        var movimientos = ConsumoDiffCalculos.Movimientos(viejos, nuevos);

        Assert.Equal(
            incrementosOriginal.OrderBy(p => p.Key.Id).Select(p => (p.Key, p.Value)),
            incrementos.OrderBy(p => p.Key.Id).Select(p => (p.Key, p.Value)));
        Assert.Equal(
            movimientosOriginal.OrderBy(m => m.Clave.Id).ThenBy(m => m.Clave.EsItemInventario),
            movimientos.OrderBy(m => m.Clave.Id).ThenBy(m => m.Clave.EsItemInventario));
    }
}
