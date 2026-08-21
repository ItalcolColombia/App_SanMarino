# Plan: mismatch front/back en `traslado-rapido` (pantalla `/traslados-aves/traslados`)

> Hallazgo original: F9 de Santa Reyes (`tracker_estado.md`, bloque V52), "de paso" mientras se
> exponia Placa/Conductor/Sellos en el traslado real de postura. Flagueado como `task_88856448`,
> sin tocar en esa sesion. Este plan es el fix, sin relacion con Santa Reyes.

## 1. El bug (confirmado con smoke HTTP real contra el backend local, no solo lectura)

`frontend/.../traslado-form/traslado-form.component.ts` arma un `TrasladoRapidoRequest`
(`traslados-aves.service.ts:194-200`): `loteOrigenId`, `loteDestinoId`, `cantidadHembras`,
`cantidadMachos`, `observaciones`.

`POST api/MovimientoAves/traslado-rapido` (`MovimientoAvesController.cs:460`, `TrasladoRapido`)
bindea `[FromBody] TrasladoRapidoRequest` — pero esa clase (linea 654, mismo archivo) es:
`LoteId` (uno solo), `GranjaOrigenId/NucleoOrigenId/GalponOrigenId`,
`GranjaDestinoId/NucleoDestinoId/GalponDestinoId`, `CantidadHembras/Machos/Mixtas`, `Motivo`,
`Observaciones`, `ProcesarInmediatamente`. Ningun nombre coincide con lo que manda el front.

**Smoke real** (backend local :5002, POST con el payload EXACTO que arma el front, JWT+SECRET_UP
de desarrollo minteados a mano — ver §5): la respuesta fue **400**, no el 500 que la lectura
estatica del codigo hacia esperar —

```json
{"status":400,"errors":{"LoteId":["The LoteId field is required."]}}
```

Motivo real: `[ApiController]` valida el modelo ANTES de que la accion corra (nullable reference
types → `string LoteId` no-nulo se infiere `[Required]`), y el controller ya chequea
`if (!ModelState.IsValid) return BadRequest(...)` (linea 464) — el request nunca llega a la linea
469 (`int.Parse(request.LoteId)`). El diagnostico original (crash 500 por
`ArgumentNullException`) era la lectura correcta del *codigo* pero no el comportamiento real en
runtime; la conclusion de fondo no cambia: **el mismatch es real, confirmado en vivo, y la
pantalla nunca completa un traslado** — solo cambia el mensaje que ve el usuario (400 "LoteId es
requerido" en vez de un 500 generico).

## 2. Por que no es un simple rename — son dos operaciones de negocio distintas

Comparando `TrasladoRapidoAsync` (`MovimientoAvesService.Traslados.cs:9-51`) contra lo que arma
el front, el backend real de este endpoint **no mueve aves entre dos lotes**: reubica **UN solo
lote** (`LoteId`) de una granja/nucleo/galpon origen a una granja/nucleo/galpon destino —
`CreateMovimientoAvesDto.LoteDestinoId` **nunca se asigna** en esa funcion. Es decir:
`TrasladoRapidoDto` no tiene forma de representar "trasladar N aves del lote A al lote B", que es
exactamente lo que pide la UI de `traslado-form` (selecciona lote origen Y lote destino).

Alinear el front al contrato real del back no es cambiar 5 nombres de propiedad: es **rehacer la
pantalla entera** (sacar el picker de "lote destino", meter pickers de granja/nucleo/galpon
destino) para terminar reconstruyendo una pantalla que **ya existe y ya funciona**.

## 3. Lo que ya existe y funciona (verificado por lectura — incluye smoke del front en 4)

| Pantalla / flujo | Operacion | Endpoint | DTOs alineados |
|---|---|---|---|
| `inventario-dashboard` → boton "Traslado" (modal inline, `abrirModalTraslado`/`procesarTraslado`) | Aves entre 2 lotes | `POST /MovimientoAves` (`createMovimiento`) | Si — `CreateMovimientoAvesDto` trae `LoteOrigenId` **y** `LoteDestinoId` (`MovimientoAvesDto.cs:74-96`) |
| `inventario-dashboard` → boton "Traslado de Lote" (`ModalTrasladoLoteComponent`, `procesarTrasladoLote`) | Reubicar UN lote (granja/nucleo/galpon) | `POST /Lote/trasladar` (`crearTrasladoLote`) | Si — es el analogo real de lo que `TrasladoRapidoAsync` dice hacer |
| `inventario-dashboard` → boton "Nuevo Traslado" (`navegarANuevoTraslado`) = ruta `/traslados-aves/nuevo` (`TrasladoAvesComponent`) | Traslado o venta de aves, lote origen + destino (lote o planta) | `POST api/traslados/aves` (`crearTrasladoAves`, `TrasladosController.cs:91`) | Si — `CrearTrasladoAvesDto` (`Application/DTOs/Traslados/CrearTrasladoAvesDto.cs`) tiene **los mismos nombres** que la interfaz TS (`LoteId`, `FechaTraslado`, `TipoOperacion`, `CantidadHembras/Machos`, `GranjaDestinoId`, `LoteDestinoId`, `TipoDestino`, `Motivo`, `Descripcion`, `Observaciones`) |
| `modal-traslado-aves-seguimiento` (desde `lote-produccion-list`/`seguimiento-lote-levante-list`) | Traslado de aves desde seguimiento diario | `POST api/Traslados/aves-desde-seguimiento` | Si (tocado y verificado en F9 de Santa Reyes) |

`TrasladosAvesService.trasladoRapido()` (el metodo que pega a `traslado-rapido`) tiene **un solo
caller en todo el repo**: `traslado-form.component.ts`. Nadie mas lo usa. Sin tests de back ni de
front sobre `TrasladoRapido*`/`traslado-form` (grep sobre `backend/tests` y `frontend/src/tests`:
0 resultados).

## 4. Decision: opcion B — deprecar `traslado-form`, redirigir la ruta

Rehacer `traslado-form` para que hable el contrato real de `TrasladoRapidoAsync` construiria una
**tercera** pantalla para "reubicar un lote", cuando `ModalTrasladoLoteComponent` (via
`Lote/trasladar`) ya lo resuelve. Y si en cambio se mantiene la intencion original de la pantalla
("trasladar aves de un lote a otro"), esa pantalla **ya existe y ya funciona**:
`/traslados-aves/nuevo`. En ningun escenario vale la pena reparar `traslado-form`.

**Fix:**
1. `app.config.ts`: la ruta hija `path: 'traslados'` (dentro de `traslados-aves`) deja de cargar
   `TrasladoFormComponent` y pasa a `redirectTo: 'nuevo'` — cualquier bookmark/menu viejo que
   apunte a `/traslados-aves/traslados` aterriza en la pantalla que si funciona, en vez de romper.
2. Borrar `frontend/src/app/features/traslados-aves/pages/traslado-form/` completo (`.ts`/`.html`/`.scss`) — 0 callers tras el paso 1.
3. `traslados-aves.service.ts`: retirar `trasladoRapido()` + interfaces `TrasladoRapidoRequest`/`TrasladoRapidoResponse` (mueren con el componente).
4. `traslados-aves.module.ts` + `traslados-aves-routing.module.ts`: **ya estan huerfanos hoy**
   (nada los importa — `app.config.ts` es el routing real, standalone; confirmado por grep). Uno
   de los dos importa `traslado-form.component`, asi que el paso 2 los deja con un import
   colgante. Se borran los dos en el mismo commit — no es ampliar el alcance, es la consecuencia
   forzada de borrar lo que importan, y ademas es exactamente la deuda de "sin NgModules" que
   CLAUDE.md ya declara superada para este stack.
5. **Backend: sin cambios.** `TrasladoRapidoAsync`/`TrasladoRapidoDto`/`TrasladoRapidoRequest`/la
   accion `TrasladoRapido` del controller quedan como estan — son consistentes puertas adentro
   (si alguien les manda el shape correcto, reubican el lote como documentan), simplemente se
   quedan sin caller en el front. Tocarlos es una decision aparte (fusionar con `Lote/trasladar` o
   borrarlos) que no hace falta para cerrar el bug reportado — ver nota en tracker.
6. `backend/sql/add_traslados_aves_menu.sql`: **no se toca.** Es un script de una sola vez, nunca
   aplicado por migracion (sin match en `Migrations/`), asi que no hay riesgo de que un menu real
   en prod siga apuntando al literal `/traslados-aves/traslados` — y si alguna vez se corrio a
   mano, la ruta ahora redirige en vez de romper.

**BD/SQL:** ninguno — es un fix de routing/limpieza de frontend, no toca contratos de datos ni
tablas.

## 5. Casos de prueba / verificacion

- **Smoke "antes" — HECHO** (confirma el bug real, no solo lectura de codigo): request HTTP real a
  `POST api/MovimientoAves/traslado-rapido` con el payload exacto que arma el front
  (`loteOrigenId`/`loteDestinoId`/`cantidadHembras`/`cantidadMachos`) → **400,
  `LoteId field is required`** (no el 500 que la lectura estatica sugeria — el `[ApiController]`
  rechaza el modelo antes de llegar al `int.Parse`; ver §1). Seguro con cualquier id (real o
  inventado): la validacion de ASP.NET corre antes de tocar la BD.
- **Build:** `dotnet build` (0 errores, sin warnings nuevos) — no hay tests de backend que tocar
  (ninguno referencia `TrasladoRapido*`). `yarn build` (0 errores) — no hay tests de frontend que
  tocar (ninguno referencia `traslado-form`/`TrasladoRapido`).
- **Smoke "despues"** (navegador real, sesion inyectada en local): navegar a
  `/traslados-aves/traslados` → confirmar que redirige a `/traslados-aves/nuevo` y la pantalla
  carga sin error (no hace falta completar un traslado real — esa pantalla es preexistente y no
  se toco su logica).
- Confirmar que ningun otro modulo importa `TrasladoFormComponent`/`TrasladosAvesModule` antes de
  borrar (grep ya hecho en la investigacion — 0 resultados fuera de si mismos).
