# Plan: Mejoras Módulo Lote Reproductora Aves de Engorde

**Fecha:** 2026-06-02  
**Feature:** Campo CodigoReproductora + Tabla enriquecida + Modal más grande + Bloqueo 7 días

---

## Requerimientos

1. **Nuevo campo `CodigoReproductora`** (editable por usuario) en los formularios del modal.
2. **Modal más grande**: ancho ~1100px, altura de scroll ampliada.
3. **Tabla enriquecida**: mostrar H/M encasetadas, H/M actuales, edad días, nº registros (n/7).
4. **Bloqueo 7 días**: si todos los lotes reproductora del lote engorde completaron 7 días, mostrar mensaje y bloquear botón "Registrar".

---

## Enfoque técnico

### Backend

**Entidad** `LoteReproductoraAveEngorde`:
- Nueva propiedad: `string? CodigoReproductora`

**Configuración EF** `LoteReproductoraAveEngordeConfiguration`:
- `.HasColumnName("codigo_reproductora").HasMaxLength(100)` (nullable)

**DTOs**:
- `LoteReproductoraAveEngordeDto`: añadir `string? CodigoReproductora = null` (al final, default null)
- `CreateLoteReproductoraAveEngordeDto`: añadir `string? CodigoReproductora = null`
- `UpdateLoteReproductoraAveEngordeDto`: añadir `string? CodigoReproductora = null`

**Service** `LoteReproductoraAveEngordeService`:
- `Map()`: incluir `CodigoReproductora = x.CodigoReproductora`
- `CreateAsync()`: asignar `CodigoReproductora`
- `UpdateAsync()`: asignar `CodigoReproductora`

**Migración EF** (idempotente):
```sql
ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS codigo_reproductora VARCHAR(100);
```

### Frontend

**Service DTO** `lote-reproductora-ave-engorde.service.ts`:
- Añadir `codigoReproductora?: string | null` a las interfaces

**Component TS** `lote-reproductora-ave-engorde-list.component.ts`:
- Añadir control `codigoReproductora` al form (create y edit)
- Getter `sieteDiasCompletosLote`: `registros.length > 0 && registros.every(r => r.sieteDiasCompletos)`
- `canCreateMore()`: bloquear también si `sieteDiasCompletosLote`
- Incluir `codigoReproductora` en los payloads save/saveBulk

**Component HTML** `lote-reproductora-ave-engorde-list.component.html`:
- Modal crear: añadir campo "Código reproductora" (editable) reordenando la sección Datos básicos en 4 columnas
- Modal editar: ídem
- Modal ver: ídem
- Tabla: añadir columnas H Encaset. / M Encaset. / Edad / Registros / H actual / M actual
- Mensaje bloqueo 7 días debajo del botón

**Component SCSS** `lote-reproductora-ave-engorde-list.component.scss`:
- `.lrae-modal__panel--create`: `max-width: 1160px`
- `.lrae-modal__panel--edit`: `max-width: 860px`
- `.lrae-create-scroll`: `height: 480px; max-height: 60vh`

---

## Archivos a modificar

| Archivo | Cambio |
|---|---|
| `ZooSanMarino.Domain/Entities/LoteReproductoraAveEngorde.cs` | + prop CodigoReproductora |
| `ZooSanMarino.Infrastructure/Persistence/Configurations/LoteReproductoraAveEngordeConfiguration.cs` | + column mapping |
| `ZooSanMarino.Application/DTOs/LoteReproductoraAveEngordeDto.cs` | + CodigoReproductora en 3 DTOs |
| `ZooSanMarino.Infrastructure/Services/LoteReproductoraAveEngordeService.cs` | Map + Create + Update |
| Migración EF nueva | ADD COLUMN IF NOT EXISTS |
| `lote-reproductora-ave-engorde.service.ts` | + interfaces |
| `lote-reproductora-ave-engorde-list.component.ts` | form + lógica 7 días |
| `lote-reproductora-ave-engorde-list.component.html` | modal + tabla |
| `lote-reproductora-ave-engorde-list.component.scss` | ancho/alto modal |
