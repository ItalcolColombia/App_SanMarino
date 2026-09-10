# Ocultar lotes/lote base cerrados en Seguimiento Diario y en Lote Management

## Objetivo (pedido del usuario)

1. **Seguimiento Diario Levante** y **Seguimiento Diario Producción**: el selector de lote NO debe
   listar un lote cuyo levante (o producción) ya está cerrado/liquidado — no hay nada que registrar
   ahí. Sin toggle: exclusión incondicional.
2. **Lote Management → tab "Lote"**: al cargar, mostrar por defecto solo lotes abiertos en AL MENOS
   una de sus dos fases (levante o producción). Un lote con **las dos fases cerradas** deja de
   aparecer. Se agrega un filtro **Abiertos / Cerrados / Todos** (default `Abiertos`) para poder
   volver a verlos, combinable con los filtros existentes (compañía/granja/núcleo/galpón/búsqueda).
3. **Lote Management → tab "Lote Base"**: al cargar, mostrar por defecto solo bases que (a) tengan
   al menos un lote abierto, o (b) todavía no tengan ningún lote asignado ("sin asignar" — no puede
   estar "cerrada" si no existe). Una base cuyos lotes están **todos** cerrados deja de aparecer.
   Mismo filtro Abiertos/Cerrados/Todos (default `Abiertos`).

## Por qué (contexto)

Hoy ninguna de las 4 pantallas filtra por estado de cierre:
- `LoteLevanteFilterDataService`/`SeguimientoProduccionController.GetFilterData` devuelven TODOS los
  lotes (abiertos y cerrados) — el usuario ve en el desplegable lotes ya liquidados y solo se entera
  de que están bloqueados al seleccionarlos (`isLoteCerrado`/`loteProduccionCerrado` ya existen y
  bloquean alta/edición/borrado, pero no ocultan la opción del combo).
- `LoteService.GetAllAsync()` (tab "Lote") y `LotePosturaBaseService.GetAllAsync()` (tab "Lote Base")
  devuelven todo lo de la empresa/granjas asignadas sin importar el cierre, así que las listas crecen
  sin límite con lotes ya liquidados.

Precedente directo en el repo: `movimientos-pollo-engorde-list.component.ts` ya filtra
`estadoOperativoLote !== 'cerrado'` para el combo de traslado-origen sin tocar la lista completa
usada para venta. Mismo patrón de "ocultar del selector, no borrar el dato".

## Alcance explícitamente EXCLUIDO

- Los sub-tabs internos `activeTab === 'levante'` / `'produccion'` de `lote-list.component`
  (habilitados solo con el flag de empresa `separaLotesPorEtapa`, apagado por defecto) — el pedido
  habla de "el lote" y "lote base", no de esos sub-tabs. No se tocan.
- `LoteService.GetAllAsync()` NO cambia su comportamiento por defecto (lo consumen
  `LoteProduccionFilterDataService`, `LoteReproductoraFilterDataService`,
  `ReporteTecnicoLevanteFilterDataService` — todos necesitan el universo completo, incluidos
  cerrados, para reportes/cascadas históricas). El filtro nuevo es 100% cliente sobre datos ya
  aditivos en el DTO — cero riesgo para esos otros consumidores.
- `LotePosturaBaseService.GetAllAsync()` tampoco cambia su filtro server-side por el mismo motivo
  (`LoteProduccionFilterDataService`, `ReporteTecnicoLevanteFilterDataService` lo consumen para
  cascadas de reporte). Solo se le agregan campos aditivos al DTO.
- No hay migración de BD: todo sale de columnas `estado_cierre` que ya existen.

## Regla de negocio nueva (cálculo puro)

`FaseLoteCalculos.EstaLoteCerradoCompleto(bool levanteCerrado, bool tieneProduccion, bool produccionCerrada)`
→ `levanteCerrado && tieneProduccion && produccionCerrada`.

Un lote sin producción todavía (aunque el levante esté cerrado) NO cuenta como "cerrado completo":
sigue siendo un estado a medio transicionar y debe seguir visible (evita esconder un dato raro/roto).
Vive junto a `ResolverFaseVisible` en `FaseLoteCalculos.cs` porque combina las mismas señales
(`LevanteCerrado`, `TieneProduccion`) + una tercera (`ProduccionCerrada`, nueva).

Para "Lote Base": una base se muestra por defecto si `TotalLotes == 0` (sin asignar) **o**
`TieneLoteAbierto == true` (existe al menos un lote hijo con `!EstaLoteCerradoCompleto`). Función
pura en el front: `mostrarLoteBasePorDefecto(totalLotes, tieneLoteAbierto)`.

## Cambios backend

### 1. `Application/Calculos/FaseLoteCalculos.cs`
Agregar `EstaLoteCerradoCompleto(bool, bool, bool)`. Tests en `FaseLoteCalculosTests.cs`
(las 8 combinaciones booleanas + el caso "levante cerrado sin producción todavía" documentado).

### 2. `Application/DTOs/Lotes/LoteDetailDto.cs`
Agregar parámetro opcional final `bool ProduccionCerrada = false` y una propiedad derivada
`CerradoCompleto` (mismo patrón que `FaseActual`, para no duplicar la fórmula).

### 3. `Infrastructure/Services/Funciones/LoteService.Consulta.cs` → `ProjectToDetail`
Agregar la subquery de `ProduccionCerrada` (mismo estilo inline que `TieneProduccion`/`LevanteCerrado`,
EF no traduce llamadas a métodos propios dentro del `Select`):
```csharp
ctx.LotePosturaProduccion.Any(p => p.LoteId == l.LoteId && p.DeletedAt == null
                                 && p.EstadoCierre != null
                                 && p.EstadoCierre.ToLower() == "cerrada")
```
Sin cambios en la firma de `GetAllAsync` ni en su filtrado — el campo es puramente aditivo.

### 4. `Infrastructure/Services/LoteLevanteFilterDataService.cs`
En `GetFilterDataAsync`, excluir de `lotes` los levante con cierre cerrado, usando la función pura ya
existente (esto SÍ corre en memoria post-`ToList()`, así que se puede llamar directo):
```csharp
var lotes = levantesDetail
    .Where(l => l.LoteId.HasValue && !CicloVidaPosturaCalculos.EstaCerrado(l.EstadoCierre))
    .Select(...)
```
Único consumidor de este service (`SeguimientoLoteLevanteController.GetFilterData`) → sin blast radius.

### 5. `API/Controllers/SeguimientoProduccionController.cs` → `GetFilterData`
Mismo criterio, sobre `lppSvc.GetAllAsync(ct)` (ya materializado):
```csharp
var lotes = (await lppSvc.GetAllAsync(ct))
    .Where(l => farmIds.Contains(l.GranjaId) && !CicloVidaPosturaCalculos.EstaCerrado(l.EstadoCierre))
    .Select(...)
```
Lógica de filtro vive inline en el controller (como ya está hoy) — único consumidor confirmado
(`lote-produccion-list.component.ts`).

### 6. `Application/DTOs/LotePosturaBaseDto.cs`
Agregar dos campos aditivos al final del record: `int TotalLotes = 0`, `bool TieneLoteAbierto = true`
(default `true` = visible; así `CreateAsync`/`UpdateAsync`, que mapean sin subquery, siguen mostrando
la base recién creada/editada — es además la respuesta correcta: una base nueva no tiene lotes
cerrados).

### 7. `Infrastructure/Services/LotePosturaBaseService.cs` → `GetAllAsync`
Extender el query LINQ con las dos subqueries correlacionadas (mismo estilo inline que
`ProjectToDetail`, mismo criterio de "no hijo de producción" que ya usa `LoteService.GetAllAsync`
para no contar el lote duplicado):
```csharp
TotalLotes = _ctx.Lotes.Count(l => l.LotePosturaBaseId == lpb.LotePosturaBaseId
                                 && l.DeletedAt == null
                                 && !(l.Fase == "Produccion" && l.LotePadreId != null)),
TieneLoteAbierto = _ctx.Lotes.Any(l => l.LotePosturaBaseId == lpb.LotePosturaBaseId
                                 && l.DeletedAt == null
                                 && !(l.Fase == "Produccion" && l.LotePadreId != null)
                                 && !( /* mismas 3 subqueries de cierre que ProjectToDetail */ ))
```
`GetByIdAsync` NO se toca (no lo necesita ningún consumidor).

## Cambios frontend

### 8. `features/lote/services/lote.service.ts`
Agregar a `LoteDto`: `produccionCerrada?: boolean`, `cerradoCompleto?: boolean` (llegan solos del
backend, mismo patrón que `levanteCerrado`/`tieneProduccion`/`faseActual` ya existentes).

### 9. `features/lote/services/lote-postura-base.service.ts`
Agregar a `LotePosturaBaseDto`: `totalLotes: number`, `tieneLoteAbierto: boolean`.

### 10. `features/lote/funciones/lote-list-encasetamiento.funcion.ts`
Nueva función pura: `mostrarLoteBasePorDefecto(totalLotes: number, tieneLoteAbierto: boolean): boolean
=> totalLotes === 0 || tieneLoteAbierto`. Test en el spec hermano del archivo (ya existe
`agrupar-huevo-items.funcion.spec.ts` como referencia de estilo en esta carpeta).

### 11. `features/lote/components/lote-list/lote-list.component.ts`
- Nuevo estado: `estadoLoteFilter: 'abiertos' | 'cerrados' | 'todos' = 'abiertos'` (tab Lote) y
  `estadoBaseFilter: 'abiertos' | 'cerrados' | 'todos' = 'abiertos'` (tab Lote Base).
- `recomputeList()`, rama `else` (tab `'lote'`): aplicar el filtro sobre `l.cerradoCompleto` según
  `estadoLoteFilter`.
- Nuevo campo `basesPosturaBaseTab: LotePosturaBaseDto[] = []` (resultado del scope por granja,
  ANTES del filtro de estado) + método `recomputeLoteBaseList()` que aplica `estadoBaseFilter` sobre
  `mostrarLoteBasePorDefecto(...)` y escribe `viewLotesPosturaBase`. Se llama desde el `next` de
  `loadLoteBaseTab()` y desde el nuevo handler `onEstadoBaseFilterChange()`.
- Nuevos handlers: `onEstadoLoteFilterChange(v)` → `recomputeList()`;
  `onEstadoBaseFilterChange(v)` → `recomputeLoteBaseList()`.
- `resetListFilters()`: resetear `estadoLoteFilter` a `'abiertos'` también (mantiene el default de
  "no llenarse de lotes").

### 12. `features/lote/components/lote-list/lote-list.component.html`
- Nuevo `filter-field` (select) en el bloque `filter-fields` (junto a Ordenar/Dirección), visible
  solo cuando `activeTab === 'lote'` o `activeTab === 'loteBase'`, con label "Estado" y opciones
  Abiertos (default) / Cerrados / Todos — bind a `estadoLoteFilter` u `estadoBaseFilter` según la
  pestaña activa.

## Casos de prueba

**Backend (`FaseLoteCalculosTests.cs`, xUnit):**
- `(false,false,false)→false`, `(true,false,false)→false`, `(true,true,false)→false` (producción
  abierta todavía), `(true,true,true)→true` (el único caso verdadero), y las combinaciones restantes
  con `levanteCerrado=false` → siempre `false` sin importar el resto.

**Backend (smoke manual, local, no automatizado — no hay infraestructura de integration test para
estos controllers en el repo):**
- `GET /api/SeguimientoLoteLevante/filter-data`: un lote con levante `EstadoCierre=Cerrado` no
  aparece en `Lotes`; uno abierto sí.
- `GET /api/SeguimientoProduccion/filter-data`: mismo criterio con `EstadoCierre=Cerrada`.
- `GET /api/Lote` (tab Lote, vía `LoteController`): `LevanteCerrado`/`TieneProduccion`/
  `ProduccionCerrada`/`CerradoCompleto` en el JSON, valores correctos contra un lote con ambas fases
  cerradas vs. uno con solo levante cerrado.
- `GET /api/LotePosturaBase`: `TotalLotes`/`TieneLoteAbierto` correctos contra una base sin lotes,
  una con lotes todos cerrados, y una con al menos un lote abierto.

**Frontend (smoke manual en navegador, `yarn start`):**
- Tab Lote: con datos reales, confirmar que el filtro `Abiertos` (default) esconde el/los lotes con
  ambas fases cerradas, que `Cerrados` los trae, que `Todos` no filtra, y que se combina con
  granja/núcleo/galpón/búsqueda ya seleccionados.
- Tab Lote Base: confirmar que una base sin lotes asignados SIGUE apareciendo en `Abiertos`, que una
  con todos sus lotes cerrados desaparece de `Abiertos` y aparece en `Cerrados`.
- Seguimiento Diario Levante y Producción: confirmar que un lote recién cerrado no aparece más en el
  combo tras recargar la pantalla (F5), y que los abiertos siguen apareciendo normalmente.

## Riesgo / reversibilidad

Todo aditivo (nuevos parámetros opcionales al final de records existentes, nuevos campos en
interfaces TS, nuevas subqueries `.Any()`/`.Count()` de solo lectura). Sin migraciones, sin cambios
de contrato para consumidores existentes de `GetAllAsync()` de lote/lote-base (siguen recibiendo el
universo completo; el recorte es 100% cliente en `lote-list.component`). El único cambio de
**comportamiento observable** son los dos `filter-data` de seguimiento diario, que dejan de listar
lotes cerrados — exactamente lo pedido, y de bajo riesgo por tener un único consumidor confirmado
cada uno.
