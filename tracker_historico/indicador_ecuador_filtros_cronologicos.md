# Histórico — Filtros Cronológicos (Año-Corrida) + Filtro Dinámico YYCC · Indicador Ecuador

**Feature:** Indicador Ecuador — Filtros Cronológicos + Filtro Backend por Ciclo de Lote  
**Estado Final:** ✅ COMPLETA  
**Fecha de Cierre:** 2026-05-08

---

## Parte 1 — Filtros Cronológicos (Año-Corrida) en el Selector de Lotes

### Especificación
Sistema de búsqueda simplificado basado en nomenclatura estándar de lotes (Año-Corrida).  
Permite localizar grupos de lotes mediante combinación Año (2 dígitos) + Corrida (01-12) = CodigoBusqueda (ej: "2601").

### Componentes implementados

**Frontend (`indicador-ecuador-list.component.ts`):**
- Propiedades: `selectedAnio`, `selectedCorrida`, `corridasDisponibles`
- Getter: `aniosDisponibles` — extrae años únicos de `peAllLotesAveEngorde` (patrón AACC, primeros 2 dígitos)
- Getter: `loteConvertido` — concatenación Año+Corrida (getter regular, NO signal, para evitar valores stale en zone-based Angular)
- Métodos: `getCodigoBusqueda()`, `aplicarFiltroCronologico()`, `onFiltroAnioChange()`, `onFiltroCorreidaChange()`
- Integración en cascada: `applyPeCascade()` aplica filtro cronológico al final

**Frontend (`indicador-ecuador-list.component.html`):**
- Fila 1: Granja / Núcleo / Galpón
- Fila 2 (condicional): Año / Corrida / "Lote a buscar" — visible solo cuando `peTodosLotesLiquidados` activo
- Campo "Lote a buscar": input deshabilitado (readonly) que muestra la concatenación Año+Corrida

### Decisiones técnicas clave
- **Signals vs getter regular:** Se usó `get loteConvertido(): string` (getter regular) en lugar de `computed()` signal porque en componentes zone-based de Angular, `computed()` puede retornar valores stale antes de que termine el ciclo de change detection. El getter recalcula siempre de forma síncrona.
- **`peAllLotesAveEngorde` para `aniosDisponibles`:** Getter lee del array completo (no del filtrado), para que los años aparezcan antes de seleccionar granja.

---

## Parte 2 — Filtro Dinámico por Ciclo de Lote (YYCC) — Backend + Frontend

### Especificación
Nuevo modo `TodosLiquidados` en el endpoint `POST /api/IndicadorEcuador/liquidacion-pollo-engorde-reporte`.  
Permite filtrar masivamente lotes liquidados por prefijo cronológico derivado del Año y Corrida (YYCC).

### Cambios implementados

**Backend — DTO (`IndicadorEcuadorDto.cs`):**
```csharp
public record LiquidacionPolloEngordeReporteRequest(
    string Modo,
    // ... campos existentes ...
    string? GalponId = null,
    string? LoteCodigo = null   // NUEVO: prefijo YYCC del nombre del lote
);
```

**Backend — Service (`IndicadorEcuadorService.cs`):**
Nuevo bloque modo `"TodosLiquidados"`:
- Obligatorio: `GranjaId`
- Opcional: `NucleoId`, `GalponId`, `LoteCodigo`
- Filtrado: `LoteNombre.StartsWith(LoteCodigo)` si viene; todos los liquidados si es nulo
- Solo lotes con aves = 0 (liquidados)
- Lanza `InvalidOperationException` si no hay resultados

**Frontend — Service (`indicador-ecuador.service.ts`):**
```typescript
export interface LiquidacionPolloEngordeReporteRequest {
  modo: 'UnLote' | 'Rango' | 'TodosLiquidados';  // NUEVO: TodosLiquidados
  // ...
  loteCodigo?: string | null;  // NUEVO
}
```

**Frontend — Component TS (`indicador-ecuador-list.component.ts`):**
- `generarLiquidacionPolloEngorde()`: cuando `peTodosLotesLiquidados`, envía `modo: 'TodosLiquidados'` y `loteCodigo: this.loteConvertido || null`
- `onPeTodosLotesChange()`: limpia `selectedAnio` y `selectedCorrida` al desmarcar
- `onPolloModoChange()`: limpia `selectedAnio` y `selectedCorrida` al cambiar de modo

**Frontend — HTML (panel filtros):**
- Checkbox "Todos los lotes liquidados" sube encima de Fila 2
- Fila 2 (Año/Corrida/Lote a buscar) tiene `*ngIf="peTodosLotesLiquidados"` — oculto por defecto

### Flujo de usuario final
```
1. Usuario selecciona Granja → Checkbox se habilita
2. Marca "Todos los lotes liquidados" → aparece Fila 2 (Año/Corrida/Lote a buscar)
3. Selecciona Año "26" + Corrida "01" → "Lote a buscar" muestra "2601"
4. Pulsa "Generar liquidación"
   → POST { "modo":"TodosLiquidados", "granjaId":40, "loteCodigo":"2601", ... }
5. Backend: LoteNombre.StartsWith("2601") + AvesActuales == 0
6. Al desmarcar o cambiar de modo → Año y Corrida se limpian automáticamente
```

### Payload JSON ejemplo
```json
{
  "modo": "TodosLiquidados",
  "alcance": "Granja",
  "granjaId": 40,
  "nucleoId": "723809",
  "galponId": null,
  "loteCodigo": "2601"
}
```

### Criterios de aceptación ✅
- Backend no falla si `loteCodigo` viene nulo
- Frontend limpia Año/Corrida al cambiar modo o desmarcar el checkbox
- Input "Lote a buscar" bloqueado pero con valor calculado visible
- Build: 0 errores TypeScript/Angular

---

## Archivos modificados

| Archivo | Tipo de cambio |
|---|---|
| `backend/.../DTOs/IndicadorEcuadorDto.cs` | Agregar `LoteCodigo` al record request |
| `backend/.../Services/IndicadorEcuadorService.cs` | Nuevo modo `TodosLiquidados` |
| `frontend/.../services/indicador-ecuador.service.ts` | Tipo modo + campo `loteCodigo` |
| `frontend/.../indicador-ecuador-list.component.ts` | Lógica `TodosLiquidados` + resets |
| `frontend/.../indicador-ecuador-list.component.html` | Condicional `*ngIf="peTodosLotesLiquidados"` |
