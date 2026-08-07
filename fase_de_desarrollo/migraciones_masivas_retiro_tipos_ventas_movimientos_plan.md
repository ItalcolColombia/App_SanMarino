# Plan — Migraciones Masivas: retirar los tipos «Ventas / Movimiento de Aves / Movimiento de Huevos»

**Fecha:** 2026-08-07
**Pedido:** «acomodar más el módulo de migración manual: ventas y traslados ya se hacen desde el
seguimiento diario, así que quitamos las cajitas de Movimiento y Ventas».

---

## 1. Diagnóstico (el código manda)

Los tres tipos son **placeholders muertos de la Fase 3**: nunca se implementaron y el catálogo los
publica con `Disponible=false` (tile «Próximamente», deshabilitado).

| Tipo | Estado real en el backend |
|---|---|
| `Ventas` | Sin esquema, sin plantilla, sin procesador. `Disponible=false`. |
| `MovimientoAves` | Ídem. |
| `MovimientoHuevos` | Ídem. |

Grep de referencias: los tres enum members **solo** aparecen en
[`TipoMigracion.cs`](backend/src/ZooSanMarino.Application/DTOs/Migracion/TipoMigracion.cs) (declaración
+ catálogo) y en un test. No participan de `GetElegiblesAsync` / `GenerarPlantillaAsync` /
`ProcesarAsync` (caen todos en el `_ => NotImplementedException`).

**Lo que sí existe ya está dentro del seguimiento** (confirma el pedido del usuario):

- `MigracionService.MovimientosAves.cs` → hoja **«Movimientos Aves»** de las plantillas de
  Seguimiento Levante y Producción (Salida / Ingreso / Venta unilaterales).
- `MigracionService.MovimientosHuevos.cs` → hoja **«Movimientos Huevos»** de la plantilla de
  Seguimiento Producción (Traslado / Venta).
- `MigracionEsquemas.MovimientosAvesLevante` / `.MovimientosHuevosProduccion` son los esquemas de
  esas hojas — **no se tocan**.

⇒ Retirar los tres tipos no quita ninguna capacidad: quita tres cajitas que anuncian una fase que ya
se resolvió por otro camino.

**Fuera de alcance (queda como está):** `VentaPolloEngorde` («Venta Engorde», Fase 4) está
**implementado y en uso** (`MigracionService.VentaEngorde.cs`, `fn_migracion_venta_engorde` v2 con
despachos, peso diferido de Panamá). La venta de engorde no se registra desde el seguimiento diario,
así que su tile se conserva. Se consulta al usuario por separado.

## 2. Defecto visual del selector (mismo pedido: «acomodar»)

En la captura, los tiles quedan ilegibles: la descripción se parte en una palabra por línea y el
badge rojo se monta sobre el título.

Causa: `.tile` es un flex de 3 columnas (icono · body · meta) y `.tile__meta` contiene
`.tile__locked` con `white-space: nowrap` y el texto largo **«Sin permiso para carga masiva»**
(~200 px) dentro de una celda de grilla de `minmax(230px, 1fr)`. La columna meta se lleva más de la
mitad del ancho y aplasta al body.

Arreglo: los metadatos (Fase + badge) bajan **debajo** del texto como una fila de chips; el icono
queda a la izquierda y el body ocupa todo el ancho restante. Sin `nowrap` que empuje. Se conserva el
mensaje completo en el `title` del botón (ya estaba) y el texto de los badges tal cual.

---

## 3. Archivos a modificar

**Backend**
1. `Application/DTOs/Migracion/TipoMigracion.cs` — borrar los 3 miembros del enum y sus 3 entradas
   del catálogo `TipoMigracionCatalogo.Todos`. Actualizar el doc de `Fase` (ya no hay fase «3»).
2. `Application/Calculos/MigracionEsquemas.cs` — reescribir el mensaje del `_ =>` de `Para()` (deja
   de mencionar «Fase 3: Ventas/Movimientos»).
3. `Infrastructure/Services/Migracion/Funciones/MigracionService.Operaciones.cs` — comentario de
   cabecera y mensaje del `_ =>` de `GetElegiblesAsync`.
4. `tests/…/MigracionEsquemasTests.cs:43` — `Para_TipoSinEsquema_Lanza` usaba `TipoMigracion.Ventas`
   como tipo sin esquema; pasa a un valor no definido del enum (`(TipoMigracion)999`), que es lo que
   la prueba realmente verifica.

**Frontend**
5. `features/migraciones-masivas/models/migracion.model.ts` — sacar los 3 del union `TipoMigracionCodigo`.
6. `features/migraciones-masivas/components/selector-tipo-migracion/selector-tipo-migracion.component.ts`
   — sacar sus 3 íconos del `Record` + rehacer el layout del tile (metadatos abajo).

**Sin cambios de BD.** `migracion_masiva.tipo` es `varchar` y se persiste con `tipo.ToString()`; no
hay ordinales en juego y no puede haber filas históricas de estos tipos (nunca fueron ejecutables).
El historial además cae a `?? item.tipo` cuando un código no está en el catálogo.

## 4. Reglas de negocio

- **Refactor ≠ cambio de comportamiento**: ningún tipo implementado cambia de esquema, plantilla,
  parseo ni resultado. Solo desaparecen opciones que devolvían 501.
- Si alguien invoca `?tipo=Ventas` (URL vieja / cliente cacheado), `TryParseTipo` falla ⇒ **400
  «Tipo de migración inválido»** en vez del 501 de antes. Degradación aceptable y explícita.
- Permisos (`carga_masiva_postura` / `carga_masiva_pollo_engorde`) y su gating quedan intactos.

## 5. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | `GET /api/Migracion/tipos` | 9 tipos; ninguno con `disponible=false`; sin Ventas/MovimientoAves/MovimientoHuevos |
| 2 | Pantalla paso 1 | 6 tiles (Levante, Producción, Lotes Engorde, Seg. Engorde, Seg. Reproductora, Venta Engorde) — los 3 de estructura se siguen ocultando por `esTipoEstructura` |
| 3 | Tile sin permiso | Descripción legible en 2-3 líneas, badge «Sin permiso para carga masiva» abajo, sin solaparse con el título |
| 4 | Plantilla Seguimiento Levante | Sigue trayendo las hojas `Datos`, `Alimento` y **`Movimientos Aves`** |
| 5 | Plantilla Seguimiento Producción | Sigue trayendo `Datos`, `Alimento`, `Huevos`, **`Movimientos Aves`** y **`Movimientos Huevos`** |
| 6 | `dotnet test` | Verde, con `Para_TipoSinEsquema_Lanza` adaptado |
| 7 | Historial | Corridas viejas siguen mostrando su nombre legible |

## 6. Validación

- `cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test`.
- `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget preexistente).
- Smoke en pantalla del paso 1 (tiles) y descarga de la plantilla de Seguimiento Producción para
  confirmar que las hojas de movimientos siguen ahí.
