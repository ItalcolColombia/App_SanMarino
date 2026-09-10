# Ventas de engorde con 0 kg netos (Panamá) — cierre del hueco + corrección del dato

> Reporte de operación (9-sep-2026): *«no me está apareciendo el total de kilos vendidos en el
> apartado de pollo engorde; sí me trae las ventas mas no los kilos»*. Encargo: diagnosticar Panamá
> y **verificar que no pase en Ecuador**.

## 1. Diagnóstico (medido, no supuesto)

Fuente: copia local de prod, corte **3-sep-2026**.

La columna «Peso despacho neto (kg)» sale de `fn_seguimiento_diario_engorde` →
`despacho_peso_neto = SUM(peso_neto)` sobre `lote_registro_historico_unificado` (`VENTA_AVES`), y el
template pinta `—` cuando no es `> 0`
(`tabs-principal-engorde.component.html:297`). El reporte y la fn están bien: **el neto es 0 en el
dato**.

| | ItalcolPanama (5) | ItalcolEcuador (3) |
|---|---|---|
| Ventas vivas | 207 | 1.472 |
| `peso_bruto = peso_tara` | **206** | **0** |
| `peso_bruto` / ave | 2,357 kg | 7,210 kg |
| `peso_neto` / ave | **0,000** | 2,836 kg |
| Espejo `lote_registro_historico_unificado` | 479.162 aves / **8 kg** | 1.778.221 aves / 4.978.966 kg |

`peso_bruto` de Panamá promedia **2,357 kg/ave**: ese número **es el peso neto del pollo**, no el del
camión cargado (Ecuador, que usa los campos bien, promedia 7,21 kg/ave en bruto y 2,836 en neto).
Total digitado dentro de `peso_bruto`: **1.124.026 kg en 206 movimientos de 17 lotes**.

**Ecuador está limpio.** Su único neto en 0 es `MPE-20260401-000102` (mar-2026) con los tres pesos en
`NULL` — anterior a que el peso fuera obligatorio; no es el patrón `bruto = tara`.

**Por qué Panamá y no Ecuador:** planta entrega UNA sola cifra de kilos; el formulario pide dos
(`pesoBruto` y `pesoTara`, ambos `Validators.required`) y el operario repite el mismo número. El flag
`venta_engorde_peso_diferido` resolvió *cuándo* llega el peso, no *cuántas* cifras tiene.

**Los dos huecos que lo dejaron pasar:**

1. `MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta` rechaza `bruto < tara` pero
   **acepta `bruto = tara`** ⇒ una venta de 2.560 aves con 0 kg netos pasa front y back.
2. El detector `MOV_SIN_PESO` de `fn_auditoria_liquidacion_engorde` —que existe justo para
   «cuentan aves, 0 kg»— filtra `peso_neto IS NULL AND (peso_bruto IS NULL OR peso_tara IS NULL)`.
   Con `peso_neto = 0` y ambos pesos digitados **no dispara**: la auditoría del lote 163 (20
   despachos, 45.479 aves, 0 kg) devuelve sólo `EXCEL_INCOMPLETO` y `AJUSTE_ALTO`.

**Arrastre:** todo lo que lee `despacho_peso_neto` queda en 0 para Panamá — tabla diaria,
`ventas_kg` de `fn_informe_semanal_pollo_engorde`, liquidación e indicadores (kilos en pie, peso
promedio de venta, conversión alimenticia).

## 2. Enfoque arquitectónico

Tres piezas independientes, en orden de dependencia inversa (primero el gate, para que la corrección
no se vuelva a ensuciar):

- **A — Gate de escritura.** La regla «una venta con peso declarado no puede pesar 0 kg netos» es
  **lógica pura** ⇒ vive en `Application/Calculos/MovimientoPolloEngordeCalculos.cs`, que ya es el
  único dueño de la validación de peso y al que llaman los **cuatro** caminos de venta
  (Panamá, `Crud.CreateAsync`, `VentaGranja`, `RegistrarPesoFactura`). El front espeja el mensaje
  para no gastar un round-trip.
- **C — Detector.** Ampliar `MOV_SIN_PESO` a `COALESCE(peso_neto,0) = 0`, que cubre el caso NULL
  anterior **y** el nuevo. Va **por migración** (regla: el `.sql` es el espejo, la migración el
  vehículo).
- **B — Corrección del dato.** Migración EF idempotente con **tabla de respaldo previa**, acotada a
  `company_id = 5` + `peso_bruto = peso_tara` + `peso_neto = 0`. El espejo
  `lote_registro_historico_unificado` se actualiza **solo**: el trigger
  `trg_movimiento_pollo_engorde_lote_hist` escucha `UPDATE OF … peso_neto, peso_tara_real,
  promedio_peso_ave …`.

## 3. Archivos

| Archivo | Cambio |
|---|---|
| `Application/Calculos/MovimientoPolloEngordeCalculos.cs` | `ValidarPesoObligatorioEnVenta`: rechazar `bruto = tara` (neto 0). |
| `tests/…/MovimientoPolloEngordeCalculosTests.cs` | Casos nuevos + equivalencia de los previos. |
| `movimientos-pollo-engorde/.../modal-venta-panama.component.ts` | Mismo mensaje antes de enviar. |
| `movimientos-pollo-engorde/.../modal-registro-peso.component.ts` | Ídem (`mensajeInvalido`). |
| `backend/sql/fn_auditoria_liquidacion_engorde.sql` | Espejo del detector ampliado. |
| `backend/sql/backfill_venta_engorde_panama_neto_cero.sql` | Espejo legible del backfill. |
| `Infrastructure/Migrations/<ts>_FixVentaEngordePanamaNetoCero.cs` | Detector + respaldo + backfill. |

## 4. Reglas de negocio

- Una venta **con peso declarado** debe tener `neto > 0`. Sin peso declarado (báscula diferida,
  ambos campos vacíos) sigue siendo legal — no se toca ese camino.
- La corrección **no inventa kilos**: mueve a `peso_neto` el número que el operario ya había
  digitado, y deja `peso_tara = 0` («no se reportó tara»), que es la verdad de lo que hay.
- Sólo se corrigen facturas de **una línea** (las 206 lo son) ⇒ el prorrateo es la identidad y no
  hay residuo de redondeo que repartir.
- **Fuera de alcance:** `MPE-20260831-002127` (bruto 5.818 / tara 5.810 ⇒ neto 8 kg). Es otro error
  de digitación pero no sabemos qué quiso poner el operario: se reporta, no se toca.

## 5. Casos de prueba

- xUnit: `bruto = tara` lanza; `bruto > tara` pasa; `bruto < tara` sigue con **su mensaje previo**;
  sin peso + diferido pasa; sin peso + no diferido lanza (mensajes byte a byte iguales a hoy).
- SQL antes/después: `peso_neto/ave` de Panamá pasa de 0,000 a ≈2,357 y **Ecuador no se mueve**
  (1.472 filas, 4.978.966 kg idénticos).
- Espejo: `lote_registro_historico_unificado` de Panamá pasa de 8 kg a ≈1.124.034 kg **sin tocarlo
  a mano** (lo hace el trigger).
- `fn_seguimiento_diario_engorde(163)`: los 6 días con despacho dejan de traer 0.
- `fn_auditoria_liquidacion_engorde`: con el detector ampliado, un lote con ventas de 0 kg devuelve
  `MOV_SIN_PESO`; corrido **después** del backfill vuelve a no devolverlo.

---

# Ampliación (10-sep-2026): liquidaciones congeladas + aviso en rojo

Pedido del usuario tras el primer entregable: **(1)** corregir también los lotes ya liquidados y que
eso vaya **por migración** para que se aplique en producción; **(2)** confirmar que lo anterior
también está en migración; **(3)** que el campo que sale vacío avise **en rojo** que falta el peso
tara, para que se entienda por qué no hay kilos.

## 6. Liquidaciones congeladas — corregir, no re-liquidar

La tabla diaria de un lote liquidado **no se calcula**: `fn_seguimiento_diario_engorde` arranca con
`SELECT … FROM liquidacion_lote_engorde_congelada_fila … UNION ALL <cálculo vivo>`, así que con copia
vigente devuelve la **foto**. Por eso el backfill de `movimiento_pollo_engorde` no les mueve un kilo.

**Dos caminos, y el motivo de elegir el segundo:**

| | `fn_recongelar_liquidacion_engorde` | UPDATE de las 3 columnas del despacho |
|---|---|---|
| Kilos corregidos | sí | sí |
| Filas tocadas fuera de los kilos | **57 de 171** (saldo aves, saldo alimento, consumo, mortalidad) | **0** (medido) |
| Motivo | regenera la copia entera con la fn de HOY (v18) y las copias son v13/v15 | sólo escribe lo que estaba mal |
| Resumen de cabecera | lo pierde (la fn SQL no lo llena; lo llena el C#) | intacto — nunca dependió de los kilos de venta |

Se elige el UPDATE quirúrgico: **corregir los kilos no es re-liquidar**. La cabecera conserva su
resumen aprobado, el `checksum` se recalcula con la MISMA expresión de `fn_congelar_liquidacion_engorde`
(que sobre un lote congelado lee justo estas filas ya corregidas, así que vuelve a describir el
contenido) y queda rastro en `metadata->'correccionKilosVenta'`.

**Guardas del UPDATE:** copia vigente · la fila hoy trae 0 kg · el día trae kilos · y las aves H/M/mixtas
de la fila coinciden EXACTO con las del día. Un día con dos seguimientos son dos filas y la fn le pone
a las dos el total del día (`LEFT JOIN … ON fecha`); el UPDATE junta por fecha y reproduce eso.

## 7. Aviso en rojo donde el dato falta

Un día con aves despachadas y 0 kg dejaba **un guion mudo** en las dos celdas de kilos: no había forma
de distinguir «ese día no hubo venta» de «la venta se cargó sin báscula». Es exactamente el reclamo que
destapó todo esto.

- **Seguimiento diario** (`tabs-principal-engorde`): las celdas «Peso despacho neto» y «Peso prom»
  muestran un badge rojo **«Falta peso tara»** cuando hay aves despachadas y 0 kg, con tooltip que dice
  cuántas aves y dónde se carga. Sin despacho el guion se queda como estaba.
- **Los 3 formularios de venta** (`modal-venta-panama`, `modal-registro-peso`,
  `modal-movimiento-pollo-engorde`): aviso rojo bajo el campo de tara cuando está vacía con el bruto
  cargado, o cuando es igual al bruto. Con báscula diferida y **ambos** campos vacíos no avisa nada:
  ese camino es legítimo.

Rojo `--danger` a propósito (la paleta lo reserva para peligro): es un dato obligatorio que falta y
arrastra al seguimiento diario, al informe semanal, a la liquidación y a los indicadores.
