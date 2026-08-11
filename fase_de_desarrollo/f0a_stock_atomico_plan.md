# Plan — F0.A · A1 + A2: el stock de inventario deja de perder escrituras

**Fecha:** 2026-08-09
**Estado:** ENTREGADO Y VALIDADO (ver §6)
**Depende de:** `pwa_offline_first_plan.md` §4.A (items **A1** y **A2**). Prerrequisito de F2/F3.

> **Esto no es trabajo "para el offline": son dos bugs de producción de HOY**, reproducibles con dos
> pestañas del navegador. El offline solo los multiplicaría por N dispositivos.

---

## 1. Los dos defectos, verificados en el código de hoy

### A1 — El stock duplicado es invisible

`InventarioGestionStockConfiguration.cs:30` declara la clave natural
`(farm_id, item_inventario_ecuador_id, nucleo_id, galpon_id)` como índice **NO único**, y todos los
caminos de escritura hacen **buscar-o-insertar** (`FirstOrDefaultAsync` → `if (null) Add(...)`).

Dos escrituras concurrentes sobre la misma ubicación e ítem no encuentran fila, y **ambas insertan**.
A partir de ahí hay dos filas de stock para la misma clave, y **todas** las lecturas usan
`FirstOrDefault`: la segunda fila queda invisible para siempre. El inventario muestra menos de lo que
hay, y el faltante no aparece en ningún reporte porque la fila existe y nadie la mira.

Sitios de buscar-o-insertar: `RegistrarIngresoAsync:475`, `RegistrarTrasladoMismaGranjaAsync:604`,
`RegistrarTrasladoConDistribucionAsync:902`, `AnularMovimientoHistoricoAsync:1224`.

### A2 — El descuento de stock es read-modify-write

`RegistrarConsumoAsync:1381-1386`:

```csharp
var stock = await _db.InventarioGestionStock.FirstOrDefaultAsync(...);
if (stock == null || stock.Quantity < req.Quantity) throw ...;   // <-- lee
stock.Quantity -= req.Quantity;                                   // <-- decide y escribe
```

Entre la lectura y el `SaveChanges` no hay nada que impida que otro consumo pase por el mismo camino.
Dos consumos de 100 sobre un stock de 150 pasan **los dos** la validación y el resultado es
**−50**: se despachó alimento que no existía. Sin transacción propia, sin `SELECT FOR UPDATE`, sin
`CHECK quantity >= 0`.

Mismo patrón en `RegistrarTrasladoMismaGranjaAsync:593`, `RegistrarTrasladoInterGranjaTransitoAsync:727`
y `RegistrarTrasladoConDistribucionAsync:882`.

---

## 2. Estado medido de la BD (local, refresh del dump de prod)

| Verificación | Resultado |
|---|---|
| Filas en `inventario_gestion_stock` | **539** |
| Grupos duplicados por clave natural | **0** |
| FKs apuntando a `inventario_gestion_stock.id` | **0** |
| Índices actuales | pkey + 3 no únicos (`farm_item_nucleo_galpon`, `company_id`, `pais_id`) |

**Cero duplicados y cero FKs** ⇒ el índice único se puede crear y consolidar filas no rompe nada.

⚠️ **Pero la migración NO puede asumirlo.** El dump local es de una fecha; prod sigue operando y con
`Database__RunMigrations=true` la migración corre **al arrancar el contenedor**. Un
`CREATE UNIQUE INDEX` contra duplicados vivos falla, la migración muere, y el arranque entra en el
modo de falla documentado en CLAUDE.md (exit 139 / rollback silencioso de ECS). Por eso la
consolidación va **en la misma migración, antes del índice, y es idempotente**.

---

## 3. Solución

### A1 · Migración `AddStockClaveNaturalUnica`

1. **Consolidar**: por cada grupo de clave natural con más de una fila, la de `MIN(id)` se queda con la
   **suma** de las cantidades y las demás se borran. Sumar es lo correcto: las filas duplicadas
   contienen stock real que entró por caminos distintos; la fila invisible representa mercadería que
   está físicamente en la granja. Se registra el efecto con `RAISE NOTICE` para que quede en el log
   del arranque.
2. **Índice único de expresión**:
   `(farm_id, item_inventario_ecuador_id, COALESCE(nucleo_id,''), COALESCE(galpon_id,''))`.
   El `COALESCE` **no es cosmético**: en Postgres `NULL <> NULL` en un índice único, así que sin él
   las filas a nivel granja (núcleo y galpón nulos, que es todo el modelo de Colombia y de las granjas
   con `maneja_alimento_por_galpon = false`) podrían duplicarse igual. EF no sabe expresar un índice
   de expresión ⇒ va por `Sql(...)`, idempotente con `IF NOT EXISTS`.
3. Se **conserva** el índice no único existente: el único de expresión no puede resolver las igualdades
   sobre `nucleo_id`/`galpon_id` que usan las consultas, así que quitarlo sería una regresión de plan.

### A2 · Descuento atómico condicional

Nueva primitiva en `Funciones/InventarioGestionService.StockAtomico.cs` (`partial class`, namespace
plano — convención de CLAUDE.md):

```sql
UPDATE inventario_gestion_stock
   SET quantity = quantity - @q, updated_at = now()
 WHERE id = @id AND quantity >= @q
```

**0 filas afectadas = rechazo**, con el mismo mensaje que hoy. La condición y la escritura ocurren en
la misma sentencia, así que el segundo consumo concurrente ve el saldo ya descontado y es rechazado
por la base, no por una validación que ya expiró.

La suma usa `INSERT ... ON CONFLICT (clave natural) DO UPDATE SET quantity = stock.quantity + EXCLUDED.quantity`:
con el índice único de A1, la carrera de dos inserciones deja **una** fila con la suma, en vez de dos
filas y una invisible.

### Transaccionalidad

El `UPDATE` crudo y el `SaveChangesAsync` que graba el movimiento tienen que ser **atómicos entre sí**:
si el movimiento fallara después del descuento, el stock bajaría sin registro que lo explique. Se abre
transacción explícita **solo si no hay una ambiente** (`_db.Database.CurrentTransaction is null`), para
no romper a los llamadores que ya envuelven la operación.

---

## 4. Casos de prueba

**Puros (xUnit) — `StockAtomicoCalculos`**
1. `filasAfectadas == 0` ⇒ rechazo con el mensaje exacto de hoy (no se cambia el texto: hay front que
   lo muestra tal cual).
2. `filasAfectadas == 1` ⇒ aceptado.
3. Cantidad ≤ 0 ⇒ rechazo antes de tocar la base.
4. Clave natural: `null` y `""` de núcleo/galpón colapsan a la **misma** clave (es lo que hace el
   `COALESCE` del índice).

**De base (SQL, en transacción + ROLLBACK)**
5. Consolidación: se insertan 3 filas duplicadas a mano, se corre el bloque, queda 1 con la suma.
6. El índice único **rechaza** la segunda inserción de la misma clave natural, incluyendo el caso de
   núcleo/galpón `NULL`.
7. El `UPDATE` condicional descuenta con saldo suficiente y **no afecta filas** con saldo insuficiente.

**De regresión**
8. `dotnet build` + `dotnet test` completos.
9. El cuadre de alimento de engorde (`GET /api/CuadreAlimentoEngorde`) no puede moverse de su estado
   actual: 61 filas / 1 descuadrado (el preexistente de Panamá).

---

## 5. Riesgos

| Riesgo | Mitigación |
|---|---|
| Duplicados vivos en prod al desplegar ⇒ migración falla ⇒ contenedor no arranca | La consolidación va **antes** del índice, en la misma migración, idempotente |
| Consolidar borra stock real | Se **suma**, no se elige un ganador; y no hay FKs que queden colgando (verificado) |
| El `UPDATE` crudo se desincroniza del change tracker de EF | Las lecturas previas al descuento pasan a `AsNoTracking()`; el tracker nunca vuelve a escribir esa fila |
| Un llamador con transacción propia | Se detecta `CurrentTransaction` y no se anida |

---

## 6. Resultado (2026-08-09)

**Entregado y validado.** Pruebas de concurrencia reales con dos sesiones psql simultáneas:

| Escenario | Antes | Ahora (medido) |
|---|---|---|
| Saldo 150, dos consumos concurrentes de 100 | los dos pasaban la validación ⇒ **−50** | A: `UPDATE 1` · B: **`UPDATE 0`** ⇒ **saldo 50** |
| Dos ingresos concurrentes sobre una clave sin fila | dos filas, una **invisible** | **1 fila con la suma** |

`dotnet build` 0 errores · `dotnet test` 2.163 verdes · migración aplicada en local (539 filas
intactas, 0 duplicados) · cuadre de alimento de engorde **61 filas / 1 descuadrado**, idéntico al
estado previo.

### Brecha abierta a propósito

`RegistrarConsumoNivelGranjaAsync` y `RegistrarIngresoNivelGranjaAsync` (Colombia, nivel granja)
**siguen con escritura diferida**. Su contrato delega el commit al orquestador, y de sus cuatro
llamadores solo tres abren transacción: el de carga masiva (`MigracionService.AlimentoPostura:131`)
no. Hacerlos atómicos hoy convertiría el descuento en un auto-commit con el movimiento pendiente —
una ventana de escritura parcial **nueva**. Prerrequisito: envolver el camino de carga masiva en su
propia transacción. Se documenta en vez de arreglarse a medias.

### Lo que sigue de F0.A

`A3` (el trigger de lotes que resetea `aves_*_actual` en la rama UPDATE), `A4` (la lectura que
escribe en `ProduccionService.ObtenerInformacionLoteAsync`), `A5` (tombstones), `A6` (índice único de
producción), `A7` (los dos services que escriben levante con semántica distinta), `A8`, `A9`, `A10`.
