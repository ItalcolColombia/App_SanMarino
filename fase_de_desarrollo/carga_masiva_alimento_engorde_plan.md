# Plan — Alimento (ingresos/traslados) en el MISMO archivo de carga masiva de seguimiento engorde

**Fecha:** 2026-07-27
**Origen:** el usuario tiene dos archivos (llegadas de alimento al galpón + seguimiento diario) y quiere
**un solo archivo** que deje el inventario cuadrado. Caso testigo: galpón 6 (`G0471`) de la granja
**DAYLAND** (id 107, `ItalcolPanama`), lote `13 - 1` (`lote_ave_engorde_id = 142`).

**Meta numérica:** que el inventario del galpón quede en **2.235,33 kg**.

---

## 0. Diagnóstico (verificado contra la BD local y el backend en :5002)

### 0.1 El balance cuadra al kilo

| Concepto | kg |
|---|---|
| Ingresos al galpón 6 (24 movimientos, 04/06 → 18/07) | 155.188,243 |
| Consumo días 1–7 (reproductora → cruce, lotes repro 61/62) | −7.166,829 |
| Consumo días 8–41 (carga masiva de seguimiento) | −145.786,084 |
| **Saldo** | **2.235,330** |

### 0.2 Estado real hoy

- El seguimiento del lote 142 **ya está cargado completo** (41 filas, 08/06 → 18/07; migración masiva
  id 29 con el archivo `…MIXTO 1 (1).xlsx`). Total consumido `149.369,496 (H) + 3.583,416 (M) = 152.952,912`.
- **El inventario está vacío**: `inventario_gestion_stock` y `inventario_gestion_movimiento` no tienen
  NI UNA fila para la granja 107. Los 155.188 kg nunca se registraron.
- `seguimiento_diario_aves_engorde.metadata` es **NULL en las 34 filas** de la carga masiva ⇒ ningún
  consumo tocó inventario.
- Los 7 días de reproductora traen `tipo_alimento = 'AV. POLLITO PREINICIADOR'` **como texto**, con
  metadata `{consumoOriginalHembras…}` sin ítems ⇒ tampoco descontaron.

### 0.3 Errores del archivo `…MIXTO 2.xlsx` (dry-run real, `POST /api/Migracion/validar`)

`Estado=ConErrores`, 2 filas con error:

| Fila | Columna | Valor | Mensaje |
|---|---|---|---|
| 4 (17/06) | Alimento 1 H | `PREINICIO` | no existe en el inventario de la empresa |
| 4 (17/06) | Alimento 2 H | `INICIO` | no existe en el inventario de la empresa |
| 10 (23/06) | Alimento 1 H | `INICIO` | no existe en el inventario de la empresa |
| 10 (23/06) | Alimento 2 H | `ENGORDE` | no existe en el inventario de la empresa |

Alimentos reales de Panamá: `SM0175 AV. POLLITO PREINICIADOR`, `SM0176 AV. SUPER POLLITO INICIACION`,
`SM0178 AV. SUPER POLLO ENGORDE` (+ variantes DORADO).

**Bug de usabilidad detectado:** el mensaje nombra la columna **`Alimento 1 H`**, que NO existe en un
archivo mixto (allí se llama `Alimento 1 Mixto`). El usuario no puede encontrar la columna que le señalan.

### 0.4 El consumo directo NO descuenta inventario

`CreateSeguimientoLoteLevanteRequest.ToDto()` solo arma `Metadata` con ítems cuando la fila trae
`Alimento 1/2`; con `Consumo Mixto (kg)` a secas, `Metadata` queda null y
`SeguimientoAvesEngordeService.CreateAsync` nunca llama a `RegistrarConsumoAsync`. **32 de 34 filas del
archivo caen en ese camino.**

### 0.5 ⚠️ El consumo va por delante de las llegadas (dato de negocio, no bug)

Reconstruyendo el kardex cronológico, el saldo **se va a negativo del 28/06 al 12/07**, con fondo en
**−10.634,13 kg el 05/07**, y vuelve a positivo el 13/07 para cerrar en 2.235,33.

Es decir: las fechas del archivo de llegadas **no son fechas de entrada física** (son de despacho o
facturación), o el galpón arrancó con un remanente del lote anterior.

**Impacto en el diseño:** `RegistrarConsumoAsync` valida contra el **stock acumulado** (`quantity`), no
contra el saldo a una fecha. Si la hoja de Alimento se procesa **entera y antes** que las filas de
seguimiento, el stock nunca baja de cero y el resultado cierra exacto. Por eso el orden de proceso es
parte del contrato, no un detalle de implementación.

### 0.6 El desglose por alimento del usuario ya es correcto

Reparto por agotamiento de fases (PREINICIO → INICIO → ENGORDE):

| Alimento | Ingresado | Semana 1 | Días 8–41 | Saldo |
|---|---|---|---|---|
| AV. POLLITO PREINICIADOR | 12.129,638 | 7.166,829 | 4.962,809 | 0,000 |
| AV. SUPER POLLITO INICIACION | 20.135,172 | — | 20.135,172 | 0,000 |
| AV. SUPER POLLO ENGORDE | 122.923,433 | — | 120.688,103 | **2.235,330** |

Las dos transiciones que el usuario ya puso en el archivo coinciden al kilo con el agotamiento real:
- **17/06** `744,353` (cierra PREINICIO: 4.218,453 acumulados + 744,353 = 4.962,806) + `1.750,431` INICIO.
- **23/06** `1.238,84` INICIO (cierra los 20.135,17) + `3.161,05` ENGORDE.

Único ajuste: el residuo de **0,069 kg** del 23/06 (`1.238,842` → `1.238,773`) para que INICIO cierre en
cero exacto y no quede negativo.

---

## 1. Enfoque arquitectónico

Hoja **`Alimento`** nueva en el MISMO `.xlsx` de `SeguimientoPolloEngorde`, procesada **antes** de la
hoja `Datos`. Aditiva: un archivo sin esa hoja se comporta **exactamente** como hoy.

```
Excel  ├── Datos          (seguimiento diario — sin cambios de contrato)
       ├── Alimento       (NUEVA: ingresos / traslados / recepciones)
       ├── Referencias    (+ catálogo de alimentos con código, ya existe)
       └── Instrucciones
```

Orden de proceso en `ProcesarSeguimientoEngordeAsync`:

1. Leer y validar la hoja `Alimento` (esquema propio).
2. Leer y validar la hoja `Datos` (igual que hoy).
3. **Simular** el balance por (granja, núcleo, galpón, ítem) partiendo del stock real: entradas de la
   hoja `Alimento` + consumos de la hoja `Datos`. Si algún ítem termina negativo → **Error** con el
   faltante exacto. Esto es lo que hoy falta: el descuento se traga las excepciones en un `catch`.
4. Si `dryRun` → cortar y devolver el reporte (incluye el saldo proyectado por alimento).
5. Si no → aplicar **primero** los movimientos de alimento y **después** las filas de seguimiento.

**Regla de oro:** refactor ≠ cambio de comportamiento. Nada de lo existente cambia de resultado; todo
lo nuevo entra por columnas/hojas opcionales.

---

## 2. Esquema de la hoja `Alimento`

| Columna | Req | Notas |
|---|---|---|
| `Fecha` | sí | `FechaMovimiento` del movimiento (ancla mediodía UTC, igual que el resto de engorde) |
| `Movimiento` | sí | `Ingreso` \| `Traslado` \| `Recepción` |
| `Alimento` | sí | nombre o código del catálogo (dropdown desde `Referencias`) |
| `Cantidad` | sí | > 0 |
| `Unidad` | no | `kg` (default) \| `qq` (×45,36) |
| `Granja` / `Núcleo` / `Galpón` | no | **destino**; vacío ⇒ la ubicación del lote seleccionado en pantalla |
| `Granja Origen` / `Núcleo Origen` / `Galpón Origen` | no | solo `Traslado` / `Recepción` |
| `Origen` | no | solo `Ingreso`: `planta` (default) \| `bodega` \| `granja` |
| `Referencia` | no | remisión / factura |
| `Observaciones` | no | va a `Reason` |

Mapeo a los servicios existentes (sin duplicar lógica de inventario):

- `Ingreso` → `IInventarioGestionService.RegistrarIngresoAsync` (ya acepta `FechaMovimiento`).
- `Traslado` → `RegistrarTrasladoAsync`. Misma granja ⇒ galpón→galpón directo; distinta granja ⇒
  crea el tránsito, igual que la pantalla.
- `Recepción` → `RegistrarRecepcionTransitoAsync` sobre el tránsito pendiente que coincida.

---

## 3. Archivos a crear / modificar

### Backend

| Archivo | Acción |
|---|---|
| `Application/Calculos/MigracionEsquemas.cs` | + `AlimentoEngorde` (esquema de la hoja nueva) |
| `Application/Calculos/MigracionAlimentoCalculos.cs` | **NUEVO** (puro): normalizar movimiento, resolver destino/origen, **simular el balance** y detectar faltantes |
| `Application/DTOs/Migracion/MigracionDtos.cs` | + `MigracionSaldoAlimentoDto` (saldo proyectado por alimento en el resultado) |
| `Application/DTOs/InventarioGestionDtos.cs` | + `FechaMovimiento` en `InventarioGestionConsumoRequest` (hoy el consumo se fecha *hoy*: el kardex histórico queda desordenado) |
| `Infrastructure/Services/InventarioGestionService.cs` | `RegistrarConsumoAsync` usa `ResolveMovimientoCreatedAt` (simetría con `RegistrarIngresoAsync`) |
| `Migracion/Funciones/MigracionService.AlimentoEngorde.cs` | **NUEVO** partial: leer/validar/aplicar la hoja `Alimento` |
| `Migracion/Funciones/MigracionService.SeguimientoEngorde.cs` | orquestar hoja `Alimento` → simulación → `Datos`; plantilla con la hoja nueva; **fix mensajes con título mixto** |
| `Migracion/Funciones/MigracionService.SeguimientoReproductora.cs` | leer `Alimento 1/2 H-M` (reusa `LeerAlimentoSlot`) |
| `Application/Calculos/MigracionEsquemas.cs` | + `Alimento 1/2 H-M` en `SeguimientoReproductoraEngorde` |

### Frontend

`migraciones-masivas`: el reporte de resultado muestra el **saldo proyectado por alimento**
(`MigracionSaldoAlimentoDto`). Sin cambios de flujo.

### Tests (xUnit, gate CI)

`MigracionAlimentoCalculosTests.cs` — nuevo:
- movimiento inválido / alimento inexistente / cantidad ≤ 0 / unidad qq
- destino heredado del lote vs explícito
- **simulación**: stock alcanza / no alcanza (faltante exacto por ítem)
- **regresión**: archivo SIN hoja `Alimento` ⇒ resultado byte a byte idéntico al de hoy

`MigracionEsquemasTests.cs` — extender con el esquema nuevo y los alias mixtos.

---

## 4. Reglas de negocio

1. **Aditivo:** sin hoja `Alimento`, comportamiento idéntico al actual.
2. **Orden:** alimento antes que seguimiento, siempre.
3. **Fail-closed:** si la simulación deja un ítem en negativo, el archivo **no se importa** (error con
   el faltante en kg). Se acabó el descuento que fallaba en silencio.
4. **Idempotencia:** el seguimiento ya la tiene por `(lote, fecha)`. Los movimientos de alimento se
   deduplican por `(granja, núcleo, galpón, ítem, fecha, cantidad, referencia)`; los repetidos suman
   a `FilasOmitidas`, no duplican stock.
5. **Empresa efectiva por datos:** la granja destino sale de `farms.company_id` de la granja del lote,
   nunca de un nombre de empresa.
6. **Unidad:** `qq` × 45,36 → kg, con la misma constante que el resto de la migración.

---

## 5. Casos de prueba (smoke del caso real)

Sobre el galpón 6 / lote 142, partiendo del inventario en cero:

| # | Caso | Esperado |
|---|---|---|
| 1 | Dry-run con hoja `Alimento` (24 filas) + `Datos` (34 filas, alimento en todas) | Validado, saldo proyectado 2.235,33 |
| 2 | Import real | stock del galpón = **2.235,33 kg** (todo en `AV. SUPER POLLO ENGORDE`) |
| 3 | Reintento del mismo archivo | 34 + 24 omitidas, stock sin cambios |
| 4 | Archivo sin hoja `Alimento` | idéntico a hoy (regresión) |
| 5 | Consumo > stock | Error con el faltante, **nada** se inserta |
| 6 | Nombre de alimento inválido | Error citando **`Alimento 1 Mixto`** (no `Alimento 1 H`) |
| 7 | Reproductora con `Alimento 1 H` | descuenta los 7.166,829 de PREINICIO |

---

## 6. Fases

- **Fase 1** — Fix de usabilidad: mensajes con el título mixto. Bajo riesgo, valor inmediato.
- **Fase 2** — Hoja `Alimento`: esquema + cálculo puro + parseo + aplicación + simulación + tests.
- **Fase 3** — Reproductora engorde con `Alimento 1/2` (primera semana descuenta).
- **Fase 4** — `FechaMovimiento` en el consumo (kardex ordenado).
- **Fase 5** — Front: saldo proyectado en el reporte.
- **Fase 6** — Armar el archivo real del galpón 6 y verificar los 2.235,33 kg punta a punta.
