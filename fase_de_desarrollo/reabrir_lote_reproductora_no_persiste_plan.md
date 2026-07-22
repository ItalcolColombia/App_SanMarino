# Plan — "Reabrir lote" reproductora engorde no persiste (confirma sin aplicar)

## Contexto / síntoma (reportado por el usuario)
En **Seguimiento Diario Reproductora Pollo Engorde**, un lote reproductora **Cerrado** muestra el botón
**"Reabrir lote"**. Al reabrir con una **novedad**, la UI dice éxito ("aparece confirmado"), pero al intentar
**eliminar** un registro el backend responde *"El lote reproductora está cerrado. Reábralo con una novedad…"*.
Es decir: **la reapertura se confirma pero no se aplica** — el flag `reabierto` nunca llega a la BD.

## Causa raíz (confirmada por lectura de código)
`LoteReproductoraAveEngordeService.ReabrirAsync` carga la entidad con una consulta con **JOIN + `AsNoTracking`**:

```csharp
var ent = await (from lrae in _ctx.LoteReproductoraAveEngorde
                 join l in _ctx.LoteAveEngorde.AsNoTracking() on lrae.LoteAveEngordeId equals l.LoteAveEngordeId!.Value
                 where l.CompanyId == companyId && l.DeletedAt == null && lrae.Id == id
                 select lrae).SingleOrDefaultAsync();
...
ent.Reabierto = true;              // ← se muta un objeto NO rastreado por el ChangeTracker
await _ctx.SaveChangesAsync();     // ← no emite UPDATE → nada se persiste
```

En EF Core, un `AsNoTracking()` presente en la consulta la vuelve **no rastreada por completo**, así que
`ent` queda **sin trackear** y `SaveChangesAsync()` es un **no-op**. El endpoint devuelve un DTO con
`reabierto=true` (mutado en memoria) que engaña al front (`puedeEliminar` pasa a true), pero la fila en BD
sigue con `reabierto=false`. Luego `SeguimientoDiarioLoteReproductoraService.DeleteAsync` **relee el valor
real** de la BD (query directa, sin join → sí rastreada) → `reabierto=false` → **lanza el guard**.

**Evidencia interna del propio repo:** el servicio hermano
`SeguimientoDiarioLoteReproductoraService.UpdateAsync` documenta y sortea exactamente este comportamiento:

> *"La entidad se cargó con una query con joins AsNoTracking → NO queda rastreada, por lo que asignar
> propiedades no emite UPDATE. Forzar el estado Modified…"* → `_ctx.Entry(ent).State = EntityState.Modified;`

`ReabrirAsync` **no** aplica esa corrección. `ConfirmarAsync` (que SÍ funciona) evita el problema cargando la
entidad **directo del DbSet sin join** → rastreada → persiste.

### Bug gemelo (mismo patrón, mismo archivo)
`LoteReproductoraAveEngordeService.UpdateAsync` (editar el maestro del lote reproductora) usa el **mismo**
patrón join+`AsNoTracking` + `SaveChanges` **sin** forzar el estado → **las ediciones del lote reproductora
tampoco persisten** (bug latente, no reportado pero idéntico). Se corrige en el mismo cambio.

## Enfoque (backend-only, mínimo, patrón ya probado en el repo)
No hay cambios de BD, ni de front, ni de contrato. Solo garantizar que la entidad esté **rastreada** antes de
`SaveChanges`. Se usan patrones que **ya existen y funcionan** en este código:

1. **`ReabrirAsync`** → mirror de `ConfirmarAsync`: validar pertenencia por compañía con un `AnyAsync`
   (join + `AsNoTracking`, sin arrastrar la entidad), y luego **cargar la entidad rastreada** con
   `_ctx.LoteReproductoraAveEngorde.SingleOrDefaultAsync(l => l.Id == id)` antes de mutar y guardar.
2. **`UpdateAsync`** → mirror de `SeguimientoDiarioLoteReproductoraService.UpdateAsync`: añadir
   `_ctx.Entry(ent).State = EntityState.Modified;` justo antes de `SaveChangesAsync()`.

`DeleteAsync` (LoteReproductora) usa `Remove(ent)` que funciona incluso con entidad no rastreada (EF la adjunta
como `Deleted`) → **no se toca**. La lógica de estado/guard/reset ya es correcta → **no se toca**.

## Archivos a modificar
1. `backend/src/ZooSanMarino.Infrastructure/Services/LoteReproductoraAveEngordeService.cs`
   - `ReabrirAsync`: cargar entidad rastreada (patrón `ConfirmarAsync`).
   - `UpdateAsync`: forzar `EntityState.Modified` antes de `SaveChangesAsync`.

## Reglas / invariantes que NO cambian
- Estado del lote (Cerrado = 7 confirmados), guard de `DeleteAsync`, auto-reset de `reabierto` al eliminar,
  aritmética de aves/saldos, contrato del DTO, y el front (botón + mini-modal de novedad + `puedeEliminar`).
- La novedad sigue obligatoria; auditoría (`reabierto_por`/`reabierto_at`) igual.

## Casos de prueba / verificación
- **Build**: `cd backend && dotnet build` → 0 errores, sin nuevas advertencias.
- **Tests**: `dotnet test` → suites existentes verdes (sin regresión).
- **E2E local (reproducir → arreglar → confirmar)** sobre BD `:5433`:
  1. Lote reproductora Cerrado (7 confirmados). Antes del fix: reabrir → `SELECT reabierto` sigue en `false`.
  2. Después del fix: reabrir con novedad → `reabierto=true`, `novedad_apertura` y `reabierto_at` poblados.
  3. Eliminar un registro del lote reabierto → **OK** (200), y el back **resetea** `reabierto=false` (recierra).
  4. Editar el lote reproductora (nombre/aves) → los cambios **sí** persisten (fix del bug gemelo).
  (E2E requiere backend vivo + token local; se corre bajo pedido si el stack está disponible.)

## Notas
- Sin migración: columnas `reabierto/novedad_apertura/reabierto_por/reabierto_at` ya existen
  (migración `20260606031251_AddReaperturaToLoteReproductoraAveEngorde`, verificado en BD local).
- El síntoma se observa en el entorno del usuario (el lote 32/`LR-6654597192` de la captura **no** está en la
  BD local → es otro entorno); el fix es de código y aplica a cualquier entorno tras desplegar.
