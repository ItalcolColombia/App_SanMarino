# Plan — Manual de usuario: Lote base (programación) de Pollo Engorde · Ecuador

**Fecha:** 2026-08-14
**Tipo:** entregable de documentación (no cambia código de producto)
**Destinatario:** usuarios con perfil **Ecuador Administrador** de ItalcolEcuador
**Credenciales de captura (LOCAL):** `admin.ecuador@italcol.com` / `123456789`

---

## 1. Objetivo

Producir un **manual con capturas reales** que explique, de punta a punta:

1. Cómo **crear un lote base** (la programación del año).
2. Cómo **asignarle granjas** y por qué sin eso no sirve.
3. Cómo **amarrar el lote base al crear un Lote de Pollo Engorde** (flujo del usuario: dónde entra, qué campos se auto-llenan).
4. Cómo **dar de baja insumos contra un lote programado** que todavía no está encasetado, y cómo esos gastos se re-atribuyen solos al encasetar.
5. Cómo **quitar una granja que ya terminó su ciclo** y **qué cambia (y qué NO) en Gestión de Inventario, Ventas y Seguimiento Diario**.

Se entrega además el **asunto + descripción corta** para el correo de entrega.

---

## 2. Verdad del sistema (auditada en código y BD local, 14ago26)

Fuente: `LoteBaseEngordeService.cs`, `LoteAveEngordeService.CreateAsync/ReatribuirGastosProgramadosAsync`,
`GastoLoteProgramadoCalculos.cs`, `lote-engorde-list.component.ts/.html`, `gastos-inventario-page.component.ts`.

| Hecho | Evidencia |
|---|---|
| El comportamiento lo prende el flag de empresa `companies.programacion_lotes_engorde`, **no el país**. Ecuador = `true` (BD local, id 3). | `companies` |
| Con el flag ON el **lote base es obligatorio** al crear lote y el **nombre del lote se calcula**; no se escribe a mano. | `aplicarProgramacionLotes()` + `Validators.required` |
| Ecuador tiene `nombre_lote_incluye_corrida = false` ⇒ el 1er lote del base+galpón se llama **`2601`**; el 2do del **mismo base y mismo galpón**, `2601 - 2`. | `recomputeNombrePorCorrida()` |
| El backend recalcula el nombre al guardar (fuente de verdad); el front solo muestra preview. | `CreateAsync` |
| En el selector "Nombre del lote" **solo aparecen bases `activo` Y asignados a la granja elegida**. | `recomputeLotesBaseParaGranja()` |
| Al **cambiar de granja** en el formulario se limpian lote base y nombre. | handler `granjaId.valueChanges` |
| Quitar una granja (`DELETE /api/LoteBaseEngorde/{id}/granjas/{farmId}`) **solo borra la fila puente**: no toca lotes ya creados, ni gastos, ni seguimiento, ni ventas. | `UnassignGranjaAsync` |
| `Desactivar` el base lo saca de **todas** las granjas a la vez (apagado global). | `SetActivoAsync` |
| `Eliminar` está **bloqueado** si hay lotes amarrados vivos o gastos programados pendientes. | `DeleteAsync` |
| El rol **Ecuador Administrador** tiene `ver/crear/editar` de `lote_base_pollo_engorde`, **no** `eliminar` ⇒ el botón de basura no le aparece. | `role_permissions` en BD local |
| Un gasto va contra lote **real** o contra **programado**, nunca ambos (CHECK en BD). | `ValidarDestino` |
| Al encasetar, se traspasan al lote real los gastos del programado con: misma granja, mismo base, mismo galpón (o gasto sin galpón) y **fecha ≤ fecha de encasetamiento**. No mueve stock. | `DebeReatribuir` / `ReatribuirGastosProgramadosAsync` |
| El selector de "lote programado" en Gastos de Inventario también filtra por **activo + asignado a la granja del gasto**. | `gastos-inventario-page.component.ts` |

**Consecuencia clave para la sección "granja que terminó su ciclo":** quitar la granja es una acción
**hacia adelante** — cierra la puerta a *nuevos* lotes y *nuevos* gastos programados en esa granja, y
**no altera ningún dato histórico**. Inventario, Ventas y Seguimiento Diario cuelgan del **lote real**
(`lote_ave_engorde_id`), no del base.

---

## 3. Escenario de captura (BD local, empresa ItalcolEcuador)

Estado inicial local: 4 lotes base (`2601`…`2604`), 28 asignaciones sobre 8 granjas, 118 lotes de engorde.

| # | Paso a capturar | Dato de la demo |
|---|---|---|
| 1 | Login y menú | `admin.ecuador@italcol.com` |
| 2 | Pestaña **Lotes base** | lista con las 4 corridas |
| 3 | Crear lote base | nombre **`2605`** |
| 4 | Modal **Asignar granjas** | asignar `Kilometro 22` |
| 5 | Crear **Lote de Pollo Engorde** | granja `Kilometro 22`, base `2605`, nombre auto |
| 6 | Gasto de inventario contra **lote programado** | concepto de desinsectación sobre `2605` |
| 7 | Re-atribución al encasetar | el gasto pasa a colgar del lote real |
| 8 | **Quitar granja** de un base con ciclo terminado | quitar `Kilometro 22` de `2605` |
| 9 | Efecto en el selector | `2605` deja de ofrecerse en `Kilometro 22` |
| 10| No-efecto | el lote ya creado sigue en Seguimiento / Inventario / Ventas |

**Limpieza:** al terminar se borran los registros creados por la demo (lote de engorde, gastos y el
lote base `2605`) y se restaura cualquier asignación tocada, para dejar la BD local como estaba.

---

## 4. Entregables (en el Escritorio)

```
Manual_Lote_Base_Engorde_Ecuador/
├── Manual_Lote_Base_Pollo_Engorde_Ecuador.docx   ← manual con capturas
├── ENTREGA_asunto_y_descripcion.md               ← asunto + descripción del correo
└── capturas/                                     ← PNG numerados del flujo real
```

---

## 5. Casos de prueba del manual (lo que se verifica en vivo antes de escribirlo)

- **P1** Crear lote base con nombre duplicado ⇒ error `Ya existe un lote base con el nombre…`.
- **P2** Base creado y **sin granjas** ⇒ no aparece en el selector de ninguna granja.
- **P3** Base asignado a `Kilometro 22` ⇒ aparece solo ahí; en otra granja no.
- **P4** Nombre del lote = nombre del base en la 1ra corrida del galpón; `- 2` en la segunda.
- **P5** Gasto contra programado ⇒ descuenta stock ya (no espera al encaset).
- **P6** Encasetar ⇒ el gasto queda atribuido al lote real y desaparece de "pendientes".
- **P7** Quitar granja ⇒ el base sale del selector de esa granja, y **el lote existente, sus
  movimientos de inventario, ventas y seguimiento diario siguen intactos**.
- **P8** Eliminar base con lotes amarrados ⇒ bloqueado con mensaje.

## 6. Validación

- Backend y front levantados en local (`:5002` / `:4200`), backend **apagado al terminar** (§ CLAUDE.md).
- Sin cambios en código de producto ⇒ no aplica `dotnet build` / `yarn build` salvo que algo obligue a tocar código.
- BD local devuelta a su estado inicial (conteos de `lote_base_engorde`, `lote_ave_engorde`,
  `inventario_gasto` y `lote_base_engorde_granja` iguales a los del arranque).
