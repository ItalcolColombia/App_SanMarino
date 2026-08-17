# Bloquear el consumo de alimento cuando no hay stock del ítem seleccionado

**Fecha:** 2026-08-17 · **Pedido del usuario:** *«en los seguimientos diarios se tiene que validar que
no se pueda realizar consumo si no se tiene stock del alimento seleccionado»*.

---

## 1. El estado actual: dos tratamientos distintos para la misma regla

| Modelo de inventario | Empresas | ¿Bloquea si falta stock? |
|---|---|---|
| `ModeloBNivelGranja` | Colombia | **SÍ.** `ValidarStockConsumoAsync` corre **antes de persistir** y lanza ⇒ rollback ⇒ el seguimiento no se guarda |
| `ModeloB` (núcleo+galpón) | Ecuador · Panamá | **NO.** El seguimiento se guarda **primero**; el consumo va después dentro de `try { … } catch { LogError }` |

`InventarioGestionService.RegistrarConsumoAsync` **sí** valida (lanza
`StockAtomicoCalculos.MensajeStockInsuficiente` cuando la fila de stock no existe o no alcanza, con un
`UPDATE … WHERE quantity >= …` atómico). El problema no es que no valide: es que **nadie escucha**.
El `catch` se come la excepción y el registro queda guardado con su consumo mientras el inventario no
se movió — el comentario del código lo llama «flujo tolerante».

**Consecuencia:** en Ecuador y Panamá se puede cargar un día de consumo de un alimento del que no hay
un solo kilo. El seguimiento queda con sus kg, el inventario intacto, y la diferencia aparece después
como descuadre — que es justo lo que el cuadre viene persiguiendo.

## 2. Censo: 10 sitios en 4 servicios

| Servicio | Alta | Edición |
|---|---|---|
| `SeguimientoLoteLevanteService.Crud` | :129 | :297 |
| `SeguimientoAvesEngordeService.Crud` | :247 | :485 |
| `SeguimientoAvesEngordeEcuadorService.Crud` | :180 | :419 |
| `SeguimientoDiarioLoteReproductoraService` | :306 | :455 |

Los ocho `catch` están en :132/:304, :251/:492, :184/:427 y —peor— reproductora escribe a
`Console.WriteLine`, así que ni siquiera queda en el log estructurado.

**Fuera de este alcance** (no son captura diaria): `MigracionService.AlimentoEngorde/AlimentoPostura`
(carga histórica, que por diseño entra con `ModoCargaHistorica`) e `InventarioGastoService` (gasto, que
ya llama a `RegistrarConsumoAsync` sin tragar el error).

## 3. Lo que falta construir

No existe un validador de stock para **modelo B con núcleo/galpón**. Sí existen:
`IColombiaInventarioConsumoService.ValidarStockConsumoAsync` (nivel granja) y
`IFarmInventoryConsumoService.ValidarStockConsumoAsync` (modelo A). Falta el tercero.

### Diseño

1. **`IInventarioGestionService.ValidarStockConsumoAsync(farmId, nucleoId, galponId, byItem, ct)`** —
   comprueba todos los ítems de una vez y lanza con un mensaje que **nombra el ítem y el faltante**,
   no un genérico. Misma forma que sus dos hermanos.
2. **La validación corre ANTES de persistir**, igual que Colombia. Hoy el bloque de modelo B está
   *después* del `CreateAsync`: mover la comprobación antes es lo que permite que el rechazo deje la
   base intacta en vez de dejar el seguimiento guardado.
3. **El `catch` deja de tragar el stock insuficiente.** Se conserva el manejo de otros fallos (para no
   convertir un problema transitorio de inventario en un 500 al guardar el día), pero el caso «no hay
   stock» ya no puede llegar ahí: lo cortó la validación previa.
4. **Mensaje al usuario, no traza técnica**: «No hay stock suficiente de *Alimento X* en el galpón
   *G0490*: se piden 750 kg y hay 120 kg». El front ya muestra el `message` de un 400.

### Lo que NO cambia
- El camino con **doble validación** (`separa == true`) no toca inventario al guardar: separa. Su
  validación de stock ya la hace `ValidarStockConsumoAsync` de Colombia al validar, y para modelo B la
  hace `RegistrarConsumoAsync` dentro de la transacción de `ValidarAsync`. Este cambio no lo altera.
- La **carga histórica** sigue entrando por `ModoCargaHistorica`.
- Colombia no se toca: ya se comporta como se pide.

## 4. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| T1 | Un ítem con stock suficiente | pasa, sin mensaje |
| T2 | Un ítem sin fila de stock en esa ubicación | rechaza, y el mensaje nombra el ítem |
| T3 | Un ítem con stock insuficiente (pide 750, hay 120) | rechaza citando pedido y disponible |
| T4 | Varios ítems, uno solo falla | rechaza, y el mensaje señala **cuál** |
| T5 | Cantidades ≤ 0 | se ignoran (no se valida lo que no se consume) |
| S1 | **Runtime**: alta de seguimiento con alimento sin stock (Panamá) | **400** con el mensaje, y **ni el seguimiento ni el inventario** cambian |
| S2 | **Runtime**: el mismo alta con stock suficiente | 201, stock descontado |
| S3 | Edición que sube el consumo por encima del stock | 400, el registro queda como estaba |
| S4 | Colombia (modelo B nivel granja) | **sin cambios** — su camino ya bloqueaba |
