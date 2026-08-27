# Liquidación Panamá: 400 sin mensaje al guardar los insumos

## Síntoma reportado

Al liquidar un lote de pollo engorde en Panamá (lote "13 - 1"), el modal muestra:

```
Http failure response for https://zootecnico.sanmarino.com.co/api/ReporteIndicadorPanama/liquidar: 400 OK
```

No pasa en Ecuador porque Ecuador **no usa este endpoint**: el flujo de liquidación de
`modal-liquidacion-lote-engorde` solo llama `POST /ReporteIndicadorPanama/liquidar` (guardar los 6
insumos) cuando `esPanama`; Ecuador va directo a `LoteAveEngordeService.CerrarLoteAsync`.

## Causa raíz

`GuardarLiquidacionPanamaRequest` tipa `AvesFinalGranja`, `AvesBeneficiada`, `DiasEngorde` y
`DiasEnGranja` como `int`. Los 4 inputs del modal para esos campos no tenían `step` ni validación de
enteros — solo exigían `> 0` (`panamaCamposCompletos`). Un decimal tipeado en cualquiera de ellos
(ej. pegar "24046.5" desde un reporte) pasa ese gate; al llegar al backend, `System.Text.Json`
rechaza la conversión a `int` **antes de que el controller/action corra** (falla en el model
binding). `[ApiController]` responde el 400 automático de siempre
(`ValidationProblemDetails: {title, errors}`) — una forma que **no tiene** `error` ni `message`.

El modal ya sabía leer `err.error.error` / `err.error.message`, pero ninguno existe en esa respuesta
→ cae al `err.message` de Angular, que SIEMPRE es el genérico "Http failure response for URL: status
statusText", sin decir nada del motivo real.

Confirmado en vivo (26-ago-2026) contra el backend local: cualquier fallo de deserialización del
body (probado con `POST /api/Auth/recover-password`, JSON malformado) produce exactamente esa forma
sin `error`/`message` — el mismo mecanismo que dispara para cualquier `[FromBody]` del app, no solo
para este endpoint.

## Fix

**Backend — `Program.cs`, `ConfigureApiBehaviorOptions`:** reescribe la respuesta automática de
`[ApiController]` a la MISMA forma `{error: "..."}` que ya usan todos los controllers, nombrando el
campo que falló (`ModelState` trae la ruta JSON + el mensaje de conversión). Cambio global — aplica
a cualquier endpoint del app, no solo a este.

**Frontend — módulo `aves-engorde`:**
- `funciones/validar-insumos-panama.funcion.ts`: valida que los 4 campos enteros de Panamá sean
  `Number.isInteger` antes de enviarlos; mensaje inmediato en rojo, sin ida y vuelta al backend.
- `funciones/extraer-mensaje-error.funcion.ts`: lee el mensaje real de un `HttpErrorResponse` —
  cubre `{error}`/`{message}` (excepciones de negocio) y `{title, errors}`
  (`ValidationProblemDetails`) antes de caer al genérico de Angular. Reemplaza las 7 cadenas
  `err?.error?.error ?? err?.error?.message ?? err?.message ?? '...'` duplicadas en
  `modal-liquidacion-lote-engorde.component.ts`.
- `modal-liquidacion-lote-engorde.component.html`: `step="1"` en los 4 inputs enteros de Panamá
  (Días en Granja, Días de Engorde, Aves Finales en Granja, Aves Beneficiadas).

Sin cambios de BD/SQL. Sin cambio de reglas de negocio: la decisión (deliberada, mismo día, commit
`6a37736`) de liquidar sin bloquear por falta de ventas registradas **no se toca** — ese banner
informativo ya existe (`avesVivasPendientes > 0`) y sigue igual.

## Casos de prueba

- Backend: request con JSON malformado a un endpoint `[FromBody]` anónimo → 400 con `{error: "..."}`
  nombrando el campo (verificado en vivo contra `/api/Auth/recover-password`).
- Frontend (`validar-insumos-panama.funcion.spec.ts`): decimal en cada uno de los 4 campos enteros
  → mensaje nombrando ESE campo; enteros válidos → `null`; campo `null` (aún sin llenar) → `null`
  (lo cubre `panamaCamposCompletos`, no este validador).
- Frontend (`extraer-mensaje-error.funcion.spec.ts`): las 3 formas de respuesta del backend
  (`{error}`, `{message}`, `{title, errors}` con 1 y con varios campos) + fallback al default del
  caller cuando no hay nada utilizable.
- `dotnet build` (backend) — 0 errores, 0 warnings.
- `yarn build` (frontend, Node portable 22.23.1) — 0 errores.

## Pendiente (no cubierto por este fix)

Smoke manual en pantalla contra un lote Panamá real liquidando con datos válidos: requiere login que
este agente no tiene — queda para que el usuario lo confirme.
