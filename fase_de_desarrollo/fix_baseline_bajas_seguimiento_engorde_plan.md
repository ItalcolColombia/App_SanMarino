# Fix — el borrado/edición de un seguimiento viejo infla el maestro de aves (pollo engorde)

**Fecha:** 2026-08-05
**Origen:** «Hallazgo aparte» del fix `3998aa2` — lote 107 (ItalcolEcuador · Kilometro 61 · G0037 ·
lote 2604) con **17 aves de más** frente a la identidad `encaset − ventas − bajas_aplicadas`.

---

## 1. Diagnóstico (auditoría contra BD local, solo lectura)

`RetiroAvesEngordeAplicador` entró en producción el **2026-07-27 17:58** (primera fila
`BAJA_SEGUIMIENTO` del histórico unificado). **Todo seguimiento creado antes nunca debitó**
`lote_ave_engorde.hembras_l/machos_l` **ni tiene fila en el histórico.**

### Causa raíz

`SincronizarAsync` tomaba el baseline (`bajasHembrasViejas/bajasMachosViejas`) de **las columnas del
seguimiento**, que el llamador calcula con `BajasDelDia`. Pero la única prueba de que un día
descontó es su **fila viva en el histórico**. Cuando ambas cosas discrepan:

| Operación sobre un seguimiento **sin fila** | Efecto en el maestro | Efecto en el histórico | Resultado |
|---|---|---|---|
| Borrado (`nuevas = 0`) | **+bajas** (acredita lo que nunca debitó) | `UpsertHistorico` no-op: `if (fila is not null)` | **maestro inflado, sin rastro** |
| Edición (`X → Y`) | −(Y−X) | crea fila con total `Y` | **conservación corrida en X** |

El no-op es silencioso: no deja fila anulada, no estampa `updated_at` y no genera auditoría, así que
el descuadre solo aparece al cruzar la conservación.

### Caso testigo — lote 107

Tres seguimientos para el **2026-07-24**, reconstruidos desde los movimientos de inventario del 07-30:

- `16:50:52` **#8652** (creado el 07-24, pre-aplicador, sin fila) → **borrado** ⇒ acreditación sin débito
- `16:51:43 → 16:52:28` **#10594** creado y borrado (no sobrevive fila alguna)
- `16:54:02` **#10595** creado: **5 H + 12 M = 17**, único con fila viva (histórico id 13096)

El desvío era exactamente 17. La secuencia exacta no es reconstruible (las filas borradas ya no
existen), pero el mecanismo está probado por código y por la ausencia total de rastro.

### Hipótesis descartadas

1. **Cruce de reproductora** — los 26 seguimientos del 107 tienen `origen_cruce = false` y
   `_backup_bajas_cruce_engorde_20260729` no tiene filas del lote. `SincronizarCruceAsync` sí revierte
   bien: las 30+ filas huérfanas de la base están **todas anuladas**.
2. **Filas anuladas o duplicadas** — las 10 `BAJA_SEGUIMIENTO` del 107 están vivas y
   `uq_lote_hist_origen` impide duplicados.
3. **Ajuste manual previo** — no había ninguno (`historial_lote_pollo_engorde` sin `Ajuste`/`AjusteResync`).

### Alcance medido

- **Descuadre actual: 0 lotes** en toda la flota, con la identidad por sexo y con la total.
- **Exposición** (seguimientos vivos sin fila = cada uno es una mina):
  **ItalcolEcuador 4.797 en 102 lotes (158.092 aves)** · ItalcolPanama 32 en 2 lotes (0 aves).

⚠️ Durante la auditoría el maestro del lote 107 **ya había sido corregido por SQL crudo externo**
(txn `52399`, 2 filas: lotes 107 y 184, sin `updated_at` ni auditoría, sobre una base recién
restaurada con `xmin` uniforme `52338`). No lo hizo esta sesión.

---

## 2. Enfoque arquitectónico

**El baseline lo manda la fila del histórico, no el llamador.** `SincronizarAsync` lo resuelve solo:
lee la fila por `(origen_tabla, origen_id)` y deriva de ahí lo aplicado. Fila ausente **o anulada** ⇒
baseline 0.

Es la misma convención que `SincronizarCruceAsync` ya usaba para revertir filas huérfanas
(`CantidadHembras`, `CantidadMachos + CantidadMixtas`), ahora unificada en un solo lugar.

**Sin migración ni backfill:** hoy no hay descuadre; el arreglo elimina la posibilidad de crearlo.

### Archivos

| Archivo | Cambio |
|---|---|
| `Application/Calculos/RetiroAvesEngordeCalculos.cs` | **+** `BaselineAplicado(RetiroAves?)` (puro) |
| `Infrastructure/Services/RetiroAvesEngordeAplicador.cs` | `SincronizarAsync` lee la fila y deriva el baseline; `UpsertHistoricoAsync` → `UpsertHistorico` (recibe la fila ya cargada, sin re-consultar) |
| `…/SeguimientoAvesEngordeEcuador/Funciones/…Crud.cs` | 3 llamadas sin `viejas` |
| `…/SeguimientoAvesEngorde/Funciones/…Crud.cs` + `…RetiroAves.cs` | 3 llamadas + wrapper sin `viejas` |
| `backend/sql/verificar_bajas_seguimiento_sin_aplicar.sql` | **+** complemento de `fn_cuadre_aves_engorde`: cohorte sin fila + huérfanas vivas |

> El desfase del maestro se mide **siempre** con `fn_cuadre_aves_engorde` (commit `75f7980`, sesión
> paralela) — una sola fórmula por número. El script nuevo no la duplica: mira el otro lado del
> invariante (qué seguimientos no tienen fila y qué filas quedaron huérfanas vivas).

### Reglas de negocio

- Fila viva ⇒ baseline = la fila (comportamiento **idéntico** al previo en todo el camino normal).
- Sin fila o anulada ⇒ baseline 0: **borrar no devuelve nada** y **editar descuenta el total nuevo**.
- Lote mixto: las mixtas de la fila vuelven al bucket "machos" porque `AplicarDelta` netea los sexos.
- `origen_id` fuera de rango `int` ⇒ no se toca el maestro (antes lo movía a ciegas, sin traza).

## 3. Casos de prueba (xUnit, gate CI)

- `BaselineAplicado`: sin fila ⇒ `(0,0)` · fila por sexo ⇒ `(H,M)` · fila mixta ⇒ `(0,X)`
- Borrar un seguimiento sin fila **no devuelve aves** (+ contraste explícito con el baseline viejo,
  que inflaba 5 H + 12 M)
- Editar un seguimiento sin fila descuenta el **total nuevo**, no el delta
- Regresión camino normal: alta+borrado simétricos por sexo **y** en lote mixto; edición con fila
  viva mueve **solo el delta**
