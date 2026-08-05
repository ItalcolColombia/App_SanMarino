# Fix — «Aves disponibles» difiere entre Seguimiento diario y Venta (pollo engorde)

**Fecha:** 2026-08-05 · **Módulo:** pollo engorde (seguimiento diario + movimientos/venta)
**Ticket:** diferencia de aves en CAROLINA y Sacachun 3A al intentar registrar una venta.

---

## 1. Novedad reportada y qué resultó ser

> «En CAROLINA galpón 4 lote 2603 el seguimiento diario dice **40** aves disponibles y la venta dice
> **33**. Las 33 deben ser las correctas, porque el seguimiento está sumando las **7** aves del lote
> 2601 que está cerrado en el mismo galpón (7 + 33 = 40).»

**La hipótesis de la suma entre lotes es falsa — es una coincidencia numérica.** Verificado contra la
BD (dump tipo-prod, `sanmarinoapplocal`):

| | Origen del número | Valor |
|---|---|---|
| Lote 2601 G4 (id 37), cerrado | `566 machos_l − 559 bajas` | **7** |
| Lote 2603 G4 (id 97) | `762 machos_l − 729 bajas` | **33** ← lo que muestra la venta |
| Lote 2603 G4 (id 97) | filas `BAJA_SEGUIMIENTO` ya aplicadas al maestro | **7** ← el otro 7 |

Los dos «7» **no tienen relación causal**: el del lote 2601 es su saldo real, y el que explica la
diferencia del 2603 son sus 5 filas `BAJA_SEGUIMIENTO` (1+1+1+1+3 machos, del 27/07 al 02/08).
Ningún cálculo cruza lotes: las dos consultas filtran por `lote_ave_engorde_id`.

**El número correcto es 40, no 33.** La venta es la pantalla equivocada.

---

## 2. Causa raíz — dos implementaciones del mismo número, una quedó sin el fix

En jul-2026 (commit `21e53ab`) se corrigió un doble descuento: el maestro `lote_ave_engorde.hembras_l /
machos_l` **ya lo descuenta** `RetiroAvesEngordeAplicador`, así que volver a restarle la mortalidad
acumulada del seguimiento cuenta esas bajas **dos veces**. El fix introdujo el cálculo puro
`AvesDisponiblesEngordeCalculos.BajasPendientesDeAplicar`: solo se resta lo que el maestro **todavía
no** tiene descontado, medido por las filas `BAJA_SEGUIMIENTO` del histórico unificado.

Ese fix se aplicó **solo al camino del seguimiento**. El camino de la venta quedó con la fórmula vieja:

| Camino | Archivo | Resta | Lote 97 |
|---|---|---|---|
| Seguimiento (widget) | `LoteReproductoraAveEngordeService.GetAvesDisponiblesAsync` :575 | `bajasPend = 729 − 7 = 722` | `762 − 722 =` **40** ✅ |
| Venta (todas) | `MovimientoPolloEngordeService.ResumenDisponibilidad.cs` :477-484 | `seg.Mort + seg.Sel + seg.Err = 729` | `762 − 729 =` **33** ❌ |

`BajasPendientesDeAplicar` tiene **un solo** consumidor productivo (`grep`: 1 hit fuera de tests).
Es exactamente el invariante **«una sola fórmula por número»** de `CLAUDE.md` incumplido: el mismo dato
calculado en dos lugares, uno se arregló y el otro divergió.

### Fuente de verdad — la grilla

`fn_seguimiento_diario_engorde(97)`, última fila (2026-08-02, edad 49 d — los «49 días» de la captura):

```
saldo_aves = 40
```

Y la identidad de conservación cierra exacta:
`13.700 encaset − 12.931 vendidas − 7 bajas aplicadas = 762 = machos_l` ✅
⇒ el maestro tiene descontadas **solo** las 7; las otras 722 no. Restar las 729 completas duplica esas 7.

### Alcance de la corrección

Los tres caminos de venta convergen en **`GetAvesDisponiblesLotesAsync`** — un único punto de arreglo:

- venta individual → `MovimientoPolloEngordeService.Crud.cs:115`
- venta por granja (la de la captura) → `MovimientoPolloEngordeService.VentaGranja.cs:37`
- venta Panamá → `MovimientoPolloEngordePanamaService.cs:59`

---

## 3. Impacto medido (BD local, réplica de ambas fórmulas sobre todos los lotes)

**50 lotes / 31.062 aves** que el seguimiento reporta y la venta se niega a despachar:

| Empresa | Lotes con diferencia | Aves ocultadas | Lotes totales |
|---|---|---|---|
| ItalcolPanama | 30 | 26.628 | 60 |
| ItalcolEcuador | 20 | 4.434 | 108 |

**49 lotes con seguimiento activo** (desde el 15/07). Los dos casos del ticket, reproducidos exactos:

| Granja | Galpón | Lote | Seguimiento | Venta |
|---|---|---|---|---|
| CAROLINA | GALPON 4 | 2603 | 40 | **33** |
| Sacachun 3A | Galpon-2 | 2602 | 194 | **0** ← no puede vender ni un ave |

Es un **bloqueo operativo**: la venta rechaza cantidades que sí existen.

---

## 4. Enfoque

Unificar la fórmula en el cálculo puro en vez de copiar el fix (que es lo que produjo la divergencia).

1. **`Application/Calculos/AvesDisponiblesEngordeCalculos.cs`** — agregar `DisponiblesPorSexo(...)`,
   puro, que encapsula la fórmula completa: base por sexo (maestro − mortalidad en caja − asignadas a
   reproductora *o* mortalidad en caja de reproductora según `sieteDiasCompletos`), menos las bajas
   **pendientes de aplicar**, menos las reservas `Pendiente`. `BajasPendientesDeAplicar` se conserva
   (pública y testeada) y pasa a ser el paso interno.
2. **`MovimientoPolloEngordeService.ResumenDisponibilidad.cs`** — cargar las filas `BAJA_SEGUIMIENTO`
   por lote (misma consulta que el seguimiento, en batch) y delegar en `DisponiblesPorSexo`.
3. **`LoteReproductoraAveEngordeService.GetAvesDisponiblesAsync`** — delegar en el mismo método.
   **Refactor sin cambio de comportamiento**: debe seguir devolviendo exactamente lo de hoy.

> Clamps: el seguimiento aplica uno solo al final, `max(0, base − bajas − pend)`; la venta aplica dos,
> `max(0, max(0, base − bajas) − pend)`. Son **equivalentes** para `pend ≥ 0` (si `base − bajas < 0`
> ambos dan 0; si es ≥ 0 son idénticos), así que unificar no altera el seguimiento.

**Fuera de alcance (no se toca):** el maestro `hembras_l/machos_l` y sus 722 bajas históricas nunca
aplicadas — la fórmula ya lo compensa y corregir datos es otro trabajo, con su propia verificación.
Tampoco se toca el front: solo muestra lo que devuelve el backend.

### Archivos

| Archivo | Cambio |
|---|---|
| `Application/Calculos/AvesDisponiblesEngordeCalculos.cs` | + `DisponiblesPorSexo` (puro) |
| `Infrastructure/…/MovimientoPolloEngorde/Funciones/…ResumenDisponibilidad.cs` | 🔴 el fix: carga `BAJA_SEGUIMIENTO` + delega |
| `Infrastructure/Services/LoteReproductoraAveEngordeService.cs` | delega (sin cambio de resultado) |
| `tests/…/AvesDisponiblesEngordeCalculosTests.cs` | + casos del ticket y de equivalencia |

**Sin migración ni SQL**: es aritmética en C#, la BD no cambia.

---

## 5. Reglas de negocio

- **R1** — El maestro ya trae descontadas las bajas con fila `BAJA_SEGUIMIENTO`; solo se resta el resto.
- **R2** — Seguimiento y venta devuelven **el mismo número** para el mismo lote, siempre.
- **R3** — Retrocompatibilidad: lote sin filas `BAJA_SEGUIMIENTO` (anterior al descuento automático) ⇒
  pendiente = total ⇒ resultado idéntico al de hoy en **ambas** pantallas.
- **R4** — Las reservas `Pendiente` (Venta/Despacho/Retiro) se siguen restando en el camino de venta.
- **R5** — Nunca negativo: clamp a 0 por sexo.
- **R6** — Sin cruce entre lotes: todo filtra por `lote_ave_engorde_id` (lo que el ticket sospechaba
  no ocurre, y la corrección no lo introduce).

## 6. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| T1 | Lote 97 del ticket: base 762 M, 729 registradas, 7 aplicadas, 0 pend | **40** (= grilla) |
| T2 | Sacachun 3A: base/bajas del lote 2602 | **194**, no 0 |
| T3 | Sin filas `BAJA_SEGUIMIENTO` (lote 37: 566 − 559) | **7** en ambas pantallas (R3) |
| T4 | Equivalencia: misma entrada ⇒ seguimiento == venta | igualdad (R2) |
| T5 | Reservas `Pendiente` > 0 | se restan (R4) |
| T6 | Bajas > base | 0, no negativo (R5) |
| T7 | `sieteDiasCompletos` true/false | usa mortalidad en caja de reproductora / asignadas |
| T8 | Bajas aplicadas como **mixtas** | consume primero hembras y luego machos (orden de `Repartir`) |

## 7. Validación

- `dotnet build` 0 errores / 0 advertencias nuevas · `dotnet test` verde (gate de CI).
- Paridad en BD: venta == seguimiento == `fn_seguimiento_diario_engorde` en los 49 lotes activos.
- **Gate multipaís** (`CLAUDE.md`): medir Ecuador **y** Panamá; ningún lote debe quedar por debajo de
  lo que muestra hoy el seguimiento, y los lotes sin `BAJA_SEGUIMIENTO` deben quedar **intactos**.
- Sin procesos huérfanos al terminar.

---

# Parte 2 — Corrección de DATOS y prevención (05-ago-2026)

Pedido: corregir también los datos en la BD (que es la de producción), que no vuelva a pasar en más
lotes de las empresas con estos módulos, y validar todo contra lo que está registrado.

## 1. Auditoría — la identidad correcta NO es la obvia

Primer intento: `maestro = encaset − ventas − bajas_aplicadas` ⇒ 9 lotes "descuadrados" en Ecuador.
**8 de ellos resultaron ser una corrección deliberada previa, no un bug.** Tienen
`hembras_l == bajas_h` y `machos_l == bajas_m` (⇒ disponibles = 0) porque son los lotes «2601» de
`fase_de_desarrollo/correccion_aves_disponibles_engorde_2601_plan.md` §2.3: lotes **cerrados y
liquidados** cuyas aves fantasma —nunca descargadas, por atribución de género imprecisa al final del
ciclo— se pusieron en 0 a propósito. Su rastro son 8 filas `historial_lote_pollo_engorde` con
`tipo_registro='Ajuste'` que suman **1.552 = exactamente el desfase medido**.

🔴 **«Corregirlos» les habría devuelto 1.552 aves fantasma a lotes liquidados.** La identidad canónica
—la que el propio `CorreccionAvesDisponiblesEngordeService` documenta en sus constantes— incluye ese
término:

```
maestro = inicio(historial) − ventas Completado − bajas BAJA_SEGUIMIENTO − ajustes fantasma
```

Con ella, los 8 lotes cuadran y quedan fuera. Quedan 6 casos, de los que **solo uno tiene efecto
visible**:

| Lote | Situación | Pantalla vs grilla | Acción |
|---|---|---|---|
| 107 · Km 61 · G1 · 2604 | maestro +17 | 23.936 vs **23.919** ❌ | **corregir** |
| 184 · SAN GUILLERMO · G1 · 2604 | +50 H / −50 M, total igual | 11.488 = 11.488 ✅ | corregir el reparto por sexo |
| 5, 7, 30, 132 | historial `Inicio` ≠ `aves_encasetadas` | ya coinciden ✅ | **no tocar** (revisión manual) |

**Los 17 del lote 107 tienen nombre y apellido:** son exactamente la fila `BAJA_SEGUIMIENTO` del 24-07
(origen_id 10595, 5 H + 12 M). La fila se escribió y su descuento nunca llegó al maestro. Ese
seguimiento se creó **retroactivamente el 30-07 16:54:02**, 6 días después de su fecha.

## 2. Corrección aplicada

`20260805150000_CorreccionMaestroAvesEngordeIdentidad` (data-only, sin cambio de schema), con guardas:
(1) solo lotes cuyo `Inicio` cuadra con `aves_encasetadas` — si no, la referencia no es confiable;
(2) nunca escribe un maestro negativo; (3) `IS DISTINCT FROM` ⇒ idempotente.

Simulada primero en transacción + `ROLLBACK`, después aplicada: **2 lotes tocados (Ecuador), Panamá 0,
0 negativos**, y re-ejecutar el SQL sobre la BD ya corregida da `UPDATE 0`.

## 3. Prevención

**a) El fallo silencioso.** El descuento de aves vivía dentro de
`catch (Exception ex) { Console.WriteLine(...) }` en los 4 puntos de los dos services de seguimiento
(alta y edición, Ecuador y carga masiva): si falla, el registro queda creado y el maestro sin
descontar, **sin traza** — a `Console` no lo lee nadie en ECS. Ahora va al `_logger` con lote y
seguimiento, igual que el `catch` de inventario de al lado.

> ⚠️ Se conserva deliberadamente que el error **no** aborte el alta del seguimiento: propagarlo haría
> fallar registros que hoy se guardan, y ese cambio de comportamiento es decisión del usuario.

**b) El detector.** `fn_cuadre_aves_engorde(p_company_id)` — hermana de `fn_cuadre_alimento_engorde`,
mismo criterio del repo: *el cuadre se mira, no se espera*. Devuelve el invariante por lote;
`cuadra = false` es un maestro que dejó de reflejar lo registrado y `referencia_confiable = false`
aísla los lotes que piden revisión manual en vez de corrección automática.

```sql
SELECT * FROM fn_cuadre_aves_engorde(NULL) WHERE NOT cuadra;
```

## 4. Estado final validado

| | Ecuador | Panamá |
|---|---|---|
| Lotes que cuadran | 104 / 108 | **60 / 60** |
| **Descuadrados** | **0** | **0** |
| Revisión manual (`Inicio` ≠ encaset) | 4 | 0 |
| Paridad pantalla vs grilla (lotes activos) | **32 / 32** | **29 / 29** |

`dotnet build` 0 errores / 0 advertencias · `dotnet test` 1.566 verdes · migraciones aplicadas en local
e idempotentes.

**Pendiente (no bloqueante):** los 4 lotes de revisión manual (5, 7, 30, 132) — su historial `Inicio`
no coincide con `aves_encasetadas` (5 y 7 dicen 50.000 contra 25.542 / 22.681 reales). Hoy muestran el
número correcto, así que no hay urgencia; corregir el `Inicio` requiere decidir cuál de los dos valores
es el bueno, y eso es una decisión de negocio.
