# Plan — `SyncPushService` (push offline PWA) sin `ModoCargaHistorica`

> ## ⚠️ REVERTIDO (27-ago-2026) — el fix descrito abajo se aplicó, se verificó y se deshizo
> El plan original (más abajo) se implementó tal cual está escrito. Antes de commitear, verificar el
> efecto real de `ModoCargaHistorica` (no solo el build) mostró dos cosas:
>
> 1. **El problema ya no existe.** `EC3.3` (25-ago) describía un registro que "nace `EN_RETRASO`" al
>    sincronizar offline >24h después. Pero `CreateAsync` fija `CreatedAt = DateTime.UtcNow` al
>    escribir (no usa `CapturadoAtDispositivo`), y **EC6** (26-ago, un día después de EC3, `94e1f9f`)
>    cambió la fórmula a `FechaLimiteValidacion = max(fecha, creación) + 1 día` con `hoy > límite`
>    estricto (`ValidacionSeguimientoCalculos.Estado`). Un registro creado HOY nunca puede tener
>    `hoy > límite` HOY, sin importar qué tan vieja sea su `fecha` — así que nunca nace vencido, con
>    o sin `ModoCargaHistorica`. Confirmado también que un push de varios días seguidos no se traba
>    entre sí: `AsegurarPuedeRegistrarDiaAsync` sólo bloquea por `EstaEnRetraso` (vencido), no por
>    "pendiente sin confirmar", y ninguno de los días recién creados en el mismo push está vencido.
> 2. **El fix, además, era peor que el problema.** `ModoCargaHistorica` no actúa sobre la fecha
>    límite: hace que `RequiereValidacionAsync()` devuelva `false`, y eso salta por completo
>    `SepararAsync` (la separación/doble validación real del módulo) en
>    `SeguimientoDiarioLoteReproductoraService.CreateAsync` — el alimento se habría descontado de
>    inmediato, sin quedar pendiente de confirmación humana, para **todo** push offline a una
>    empresa con `requiere_validacion_seguimiento_diario` (hoy sólo Panamá). Eso no es "tratar la
>    captura offline como ya ocurrida" (el propósito legítimo de `ModoCargaHistorica` en
>    `MigracionService`/`PuentePanamaService`, que importan datos ya asentados) — es apagar la
>    revisión humana que la doble validación existe para forzar, justo en el canal donde SÍ hace
>    falta.
>
> `SyncPushService.cs` quedó revertido a su estado previo (`git diff` vacío). Este plan queda como
> registro de la investigación, no como algo pendiente de aplicar — no reabrir sin un caso medido
> distinto al de EC3.3.

## Origen (contexto histórico — ver el aviso de arriba)
Hallazgo de EC3 (`tracker_estado.md`, bloque EC3.3): "la misma clase de defecto sigue abierta en el
push offline de la PWA — `Sync/SyncPushService.cs`, único escritor de seguimientos que no usa
`ModoCargaHistorica`". Verificado en código (27-ago-2026): `SyncPushService` no inyecta
`IValidacionSeguimientoService` y ningún archivo de `Services/Sync/` llama a `ModoCargaHistorica()`.

## Problema
Un día capturado offline en la PWA y sincronizado (`POST /api/Sync/push`) más de 24h después nace
`EN_RETRASO`/vencido, porque `RequiereValidacionAsync` no sabe que ese seguimiento es una captura
histórica que llega tarde por diseño (offline), no una demora real del operario. Menos grave que el
caso de EC3 (cruce de reproductora): estos registros SÍ son editables/validables desde pantalla, no
trancan un lote — pero generan el mismo ticket de soporte.

## Enfoque (mismo patrón ya probado 2 veces: `MigracionService`, `PuentePanamaService`)
- `SyncPushService`: agregar `private readonly IValidacionSeguimientoService? _validacion;`, parámetro
  opcional `IValidacionSeguimientoService? validacion = null` al final del constructor (no rompe DI
  existente ni tests que construyan el service directo).
- `PushAsync`: envolver el lote completo con
  `using var _cargaHistorica = _validacion?.ModoCargaHistorica();` — el push es SIEMPRE una captura que
  "ya ocurrió" en el dispositivo (mismo principio que el puente Panamá: "lo que trae no son capturas
  pendientes de validar"), así que corresponde al método completo, no a una operación puntual.
- Cero cambio de firma pública, cero cambio de contrato HTTP, cero migración.

## Archivos
- `backend/src/ZooSanMarino.Infrastructure/Services/Sync/SyncPushService.cs` (constructor + campo)
- `backend/src/ZooSanMarino.Infrastructure/Services/Sync/SyncPushService.cs` (`PushAsync`)

## Validación
- `dotnet build` — 0 errores.
- `dotnet test` — sin regresiones (no hay proyecto de tests de integración para `Services/Infrastructure`;
  el mismo patrón en `MigracionService`/`PuentePanamaService` tampoco tiene test unitario propio, su red
  es el smoke — igual acá).
- Smoke manual (si hay backend local levantado): push con `capturadoAtDispositivo` > 24h atrás no debe
  nacer vencido/`EN_RETRASO`.
