# Plan: limpieza de dead code backend — `TrasladoRapido*` + `ITrasladoAvesService`

> Follow-up de [[fix_traslado_rapido_aves_mismatch_plan.md]] (rama `claude/reverent-saha-d10a85`,
> commits `322c7a4`+`bd5e712`). Ese plan resolvio el mismatch front/back borrando `traslado-form`
> y dejo escrito explicitamente: "Backend: sin cambios... Tocarlos es una decision aparte
> (fusionar con `Lote/trasladar` o borrarlos) que no hace falta para cerrar el bug reportado".
> Esta es esa decision aparte.

## 0. Estado real verificado en ESTA rama antes de tocar nada

`claude/awesome-goodall-4a90a7` (== `main`, commit `08aa66b`) **no tenia** el fix del front —
`bd5e712` vive solo en el worktree/rama `claude/reverent-saha-d10a85`, sin mergear. Confirmado con
`git merge-base --is-ancestor bd5e712 main` → NO. En este punto, `traslado-form.component.ts`
seguia llamando `trasladoRapido()`, es decir la cadena backend **todavia tenia un caller** (roto,
pero caller al fin). Borrarla sin el fix del front habria cambiado un 400 por un fallo duro.

**Accion tomada:** merge de `claude/reverent-saha-d10a85` a esta rama (merge commit `9207b78`, sin
conflictos de codigo — un solo conflicto trivial en `.devpilot/events.jsonl`, log de telemetria
append-only, resuelto concatenando ambos lados, commit `c9c6583`). El worktree de esa rama estaba
limpio (todo commiteado), asi que no se perdio trabajo en progreso de nadie. Con el merge
aplicado, verificado de nuevo: `grep -r "TrasladoRapido" frontend/src` → 0 resultados,
`traslado-form/` ya no existe. **Recien aca la premisa del plan original ("cero callers") es
verdadera en esta rama.**

## 1. Que se borra y por que (opcion elegida: A — borrar, no fusionar)

Evaluadas las dos opciones del hallazgo original:

- **Opcion A (elegida) — borrar la cadena completa.** Cero callers confirmados (front y back, ver
  §2). No hay tests que la ejerciten. No hay coleccion Postman ni spec swagger estatica en
  `backend/documentacion/` (swagger es autogenerado desde el controller, desaparece solo). El
  concepto de negocio que **si** hace (reubicar un lote entre granja/nucleo/galpon) ya esta
  cubierto, vivo y con caller real, por `POST /Lote/trasladar` (`LoteController`,
  `ModalTrasladoLoteComponent` en el front).
- **Opcion B (descartada) — fusionar con `Lote/trasladar`.** Construiria/preservaria capacidad
  que **nadie llama hoy** — exactamente lo que CLAUDE.md pide evitar ("no disenes para requisitos
  hipoteticos futuros"). Ademas exige probar equivalencia de comportamiento entre dos
  implementaciones independientes que pueden haber divergido en semantica (reglas de
  validacion, efectos en inventario), riesgo innecesario para un endpoint sin usuario.

**Se borra:**

| Archivo | Que | Lineas (antes de editar) |
|---|---|---|
| `backend/src/ZooSanMarino.API/Controllers/MovimientoAvesController.cs` | Accion `TrasladoRapido` (`[HttpPost("traslado-rapido")]` + doc-comment) | 454-496 |
| `backend/src/ZooSanMarino.API/Controllers/MovimientoAvesController.cs` | Clase `TrasladoRapidoRequest` (+ doc-comment) | 651-669 |
| `backend/src/ZooSanMarino.Application/Interfaces/IMovimientoAvesService.cs` | Firma `TrasladoRapidoAsync(TrasladoRapidoDto dto)` | 34 |
| `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAves/Funciones/MovimientoAvesService.Traslados.cs` | Metodo `TrasladoRapidoAsync` (implementacion) | 9-51 — el resto del archivo (4 stubs `NotImplementedException` de otras firmas de la interfaz) **no se toca**, fuera de alcance |
| `backend/src/ZooSanMarino.Application/DTOs/MovimientoAvesDto.cs` | Clase `TrasladoRapidoDto` (+ doc-comment) | 197-222 |
| `backend/src/ZooSanMarino.Application/Interfaces/ITrasladoAvesService.cs` | Archivo completo — interfaz sin implementacion, sin registro DI, sin ningun consumidor (`TrasladosController.CrearTrasladoAves` construye el DTO inline; grep confirma 0 referencias fuera del propio archivo) | archivo entero |

**No se toca** (confirmado con grep, cero relacion con `TrasladoRapido`): `CrearTrasladoAvesDto`,
`MovimientoAvesDto`, `ResultadoMovimientoDto` — siguen usados por el resto del controller/service.
Tampoco los 4 metodos `throw new NotImplementedException(...)` que comparten archivo con
`TrasladoRapidoAsync` (`TrasladarEntreGranjasAsync`, `TrasladarDentroGranjaAsync`,
`DividirLoteAsync`, `UnificarLotesAsync`) — son deuda preexistente distinta, fuera del pedido.

## 2. Verificacion de "cero callers" (post-merge, en esta rama)

```
grep -rn "TrasladoRapido" frontend/src           → 0 resultados
grep -rn "TrasladoRapido" backend/tests          → 0 resultados
grep -rn "ITrasladoAvesService" backend          → 2 (ambas dentro de su propio archivo; sin AddScoped/AddTransient en Program.cs)
find backend/documentacion -iname "*.json"       → solo ecr-policy-iam-admin.json / ecs-taskdef-new-aws.json (AWS, sin relacion)
find backend -iname "*swagger*.json" -o -iname "openapi*.json"  → ninguno (swagger autogenerado en runtime)
```

**Deuda flagueada, no resuelta en este pase:** 5 documentos en `backend/documentacion/`
(`ANALISIS_MODULO_TRASLADO_AVES.md`, `MODULO_TRASLADO_AVES_ANALISIS_COMPLETO.md`,
`frontend-traslados-aves.md`, `ejemplos-api-traslados.md`, `typescript-models-traslados.ts`)
describen `traslado-rapido`/`TrasladoRapidoRequest` como si fueran el contrato vigente — son docs
de analisis/diseno historicas, no specs activas ni colecciones Postman, cero impacto en runtime o
CI. Quedan desactualizadas tras este borrado; no se editan en este pase (fuera del pedido explicito
de limpieza backend, y tocar 5 documentos largos es un cambio de alcance propio). Spawneado como
tarea aparte para el usuario (`task_df96f56e`).

## 3. Casos de prueba / verificacion

- `dotnet build` (0 errores, sin warnings nuevos).
- `dotnet test` (gate de CI) — ninguna prueba referencia `TrasladoRapido*`/`ITrasladoAvesService`
  hoy, asi que el borrado no debe romper ningun test existente; confirma ademas que nada mas en el
  arbol de compilacion dependia de estos tipos.
- Sin caso de prueba nuevo: esto es un borrado de codigo muerto, no una regla de negocio nueva
  (no aplica xUnit nuevo en `Calculos/`).

## 4. Nota operativa — path del worktree

Un primer intento de este pase escribio los cambios (plan, tracker, los 6 borrados de codigo) en
`C:\Users\SAN MARINO\Desktop\App_SanMarino\` (checkout PRINCIPAL) en vez de
`C:\...\.claude\worktrees\awesome-goodall-4a90a7\` (el worktree de esta sesion) — un path sin el
segmento del worktree resuelve al checkout principal, que ademas otra sesion tenia en uso (estaba
en `bd5e712`, no en `main`). Revertido con permiso explicito del usuario antes de reintentar. Desde
aca, todo path de archivo usa el prefijo completo del worktree.
