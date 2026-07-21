# Fix — Consumo de inventario Colombia multi-empresa (error 400 "no tiene equivalente")

**Fecha:** 2026-07-21 · **Módulos afectados:** Seguimiento Levante, Producción, Engorde (Colombia y Ecuador-service), Inventario Gestión (consumo Colombia)

## 1. Bug reportado

`POST /api/SeguimientoLoteLevante` con usuario **Demo** devuelve **400**:

> "El producto (catalogItemId=208) no tiene equivalente en el inventario unificado de Colombia (item_inventario_ecuador). No se puede descontar."

Payload del front: `itemsHembras: [{ catalogItemId: 0, itemInventarioEcuadorId: 208, ... }]` (ítem "Alimneto ERP" creado en el inventario nuevo por la empresa Demo).

## 2. Causa raíz (verificada en BD local)

`item_inventario_ecuador.id=208` → codigo `4000`, nombre "Alimneto ERP", **company_id=4 (Demo), pais_id=1 (Colombia)**.

`ColombiaInventarioConsumoService` tiene el scope **hardcodeado**: `CompanyColombia = 1` (Agroavicola Sanmarino). El "camino 2" (pass-through de ids del inventario nuevo) valida `CompanyId == 1 && PaisId == 1` → el ítem de Demo (company 4) se rechaza → 400.

**Defecto estructural adicional (riesgo latente para Sanmarino):** el parser (`MetadataEngordeCalculos.ParseMetadataItemsToKg` y `AcumularItemsRequestPorCatalogItem` en Producción) **aplana** `itemInventarioEcuadorId` y `catalogItemId` en un solo `int`, perdiendo el origen. El backend luego *adivina* el tipo de id según "¿existe en catalogo_items?". Los rangos de ids se solapan (catalogo_items: 61–366; item_inventario_ecuador: 1–355+), así que un ítem nuevo del inventario B cuyo id colisione con un id de catálogo se enruta al camino 1 → error 400 (o mapeo a un producto equivocado si el código coincidiera). El front YA distingue los dos tipos (contrato camino-1/2 de `buildItemPersistFields`); el backend descarta esa información.

## 3. Enfoque arquitectónico

**Preservar el tipo de id de punta a punta (sin adivinar) + scope por empresa efectiva de la granja.** Sin cambios de BD ni de front (el contrato del API no cambia; solo la interpretación interna).

1. **Clave tipada** `ItemConsumoKey (Id, EsItemInventario)` en `Application/Calculos` — `EsItemInventario=true` = id de `item_inventario_ecuador` (camino 2); `false` = id de `catalogo_items` (camino 1).
2. **Parser tipado** `MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(...)` → `Dictionary<ItemConsumoKey, decimal>`. El parser plano actual queda **intacto** (lo usan Ecuador/Panamá, con tests verdes que fijan su comportamiento).
3. **`IColombiaInventarioConsumoService`**: las 4 firmas pasan de `IReadOnlyDictionary<int, decimal>` a `IReadOnlyDictionary<ItemConsumoKey, decimal>`.
4. **`ColombiaInventarioConsumoService`**:
   - Empresa efectiva = `farms.company_id` de la granja recibida (la granja del lote; coincide con la empresa del stock y del dropdown). Se elimina la constante `CompanyColombia = 1`. `PaisColombia = 1` se mantiene (el gate aguas arriba garantiza lote Colombia; evita descuento cross-país dentro de una empresa).
   - Keys con `EsItemInventario=true` → validación directa contra `item_inventario_ecuador` (empresa efectiva + país 1), **sin** consultar catalogo_items.
   - Keys con `EsItemInventario=false` → camino 1 como hoy (catalogo_items sin scope → código → ítem B **de la empresa efectiva** + país 1). Para granjas de company 1 el resultado es idéntico al actual (no hay cambio de comportamiento Sanmarino).
   - Mensajes de error diferenciados por camino (el de catalogItemId conserva el texto actual).
5. **Cálculo puro** `ColombiaInventarioIdResolutionCalculos.Resolver` se reescribe con la clave tipada (desaparece el heurístico "ids que no existen en catalogo_items").
6. **Call sites** (ramas Colombia de create/update/delete): usar el parser/acumulador tipado en:
   - `SeguimientoLoteLevanteService.Crud.cs`
   - `ProduccionService.cs` (incluye twin tipado de `AcumularItemsRequestPorCatalogItem`)
   - `SeguimientoAvesEngordeService.Crud.cs`
   - `SeguimientoAvesEngordeEcuadorService.Crud.cs`
   Las ramas Ecuador/Panamá (parser plano + `_inventarioGestionService`) **no se tocan**.

## 4. Archivos a crear/modificar

| Acción | Archivo |
|---|---|
| Crear | `Application/Calculos/ItemConsumoKey.cs` |
| Modificar | `Application/Calculos/MetadataEngordeCalculos.cs` (+parser tipado) |
| Modificar | `Application/Calculos/ColombiaInventarioIdResolutionCalculos.cs` (contrato tipado) |
| Modificar | `Application/Interfaces/IColombiaInventarioConsumoService.cs` (firmas + docs) |
| Modificar | `Infrastructure/Services/ColombiaInventarioConsumoService.cs` (empresa efectiva + resolución tipada) |
| Modificar | `Infrastructure/Services/SeguimientoLoteLevante/Funciones/SeguimientoLoteLevanteService.Crud.cs` |
| Modificar | `Infrastructure/Services/ProduccionService.cs` |
| Modificar | `Infrastructure/Services/SeguimientoAvesEngorde/Funciones/SeguimientoAvesEngordeService.Crud.cs` |
| Modificar | `Infrastructure/Services/SeguimientoAvesEngordeEcuador/Funciones/SeguimientoAvesEngordeEcuadorService.Crud.cs` |
| Modificar | `tests/ZooSanMarino.Application.Tests/ColombiaInventarioIdResolutionCalculosTests.cs` |
| Modificar | `tests/ZooSanMarino.Application.Tests/MetadataEngordeCalculosTests.cs` (+tests parser tipado) |
| Modificar | `tests/ZooSanMarino.Application.Tests/SeguimientoEngordeColombiaInventarioTests.cs` (ramas Colombia tipadas) |

**BD/SQL:** ninguno. **Front:** ninguno.

## 5. Reglas de negocio

- Un ítem del inventario nuevo (`itemInventarioEcuadorId`) se descuenta si existe para **la empresa de la granja del lote** y país Colombia; ya no se exige company 1.
- Un `catalogItemId` histórico se sigue resolviendo por código al espejo B de la empresa de la granja (Sanmarino: idéntico a hoy).
- Ids ambiguos ya no existen: el tipo viaja explícito. Colisión numérica catálogo↔inventario deja de importar.
- Registros históricos (metadata vieja con solo `catalogItemId`) siguen resolviendo por camino 1 en ediciones/devoluciones.

## 6. Casos de prueba

1. **Camino 2 multi-empresa:** key `(208, EsItemInventario=true)` con ítem B válido de la empresa efectiva → resuelve a 208 (bug del Demo).
2. **Colisión ya no rompe:** key `(5, true)` resuelve directo aunque exista catalogo_items id=5; key `(5, false)` resuelve por código; ambos en el mismo request.
3. **Camino 1 intacto:** `(5, false)` con espejo por código → id B espejo; sin espejo → NO resuelve (error).
4. **Normalización de código** (trim + case-insensitive) se conserva.
5. **Parser tipado:** itemsHembras/Machos/Generales, prioridad `itemInventarioEcuadorId>0`, fallback `catalogItemId`, acumulación por clave, conversión g→kg, ids ≤ 0 ignorados.
6. **Diff tipado create/update/delete** (réplica de las ramas Colombia) con mezcla de claves A y B.
7. `dotnet build` sin errores nuevos + `dotnet test` verde.
