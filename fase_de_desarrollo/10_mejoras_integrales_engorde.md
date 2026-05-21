# Plan 10 — Fase de Mejoras Integrales: Módulo Pollo de Engorde

**Fecha de inicio:** 2026-05-21  
**Estado:** Planificado — pendiente de ejecución  
**Tracker:** `tracker_estado.md`

---

## Resumen Ejecutivo

Tres mejoras conectadas sobre el módulo de Pollo de Engorde que corrigen problemas de inventario, de visibilidad en liquidaciones y de trazabilidad logística del lote:

| # | Módulo | Problema | Solución |
|---|--------|----------|----------|
| R1 | Filtro Liquidación Ecuador | El checkbox "aves = 0" oculta lotes cerrados con residual | Selector dual (Radio Buttons): por estado vs. por saldo físico |
| R2 | Ventas / Despacho Multi-Lote | El peso global se duplica en cada lote del camión | Prorrateo proporcional: guardar pesos reales por lote en DB |
| R3 | Lote Engorde - Identificación | No existe registro de cuándo se alistó el galpón | Nuevo campo `fecha_alistamiento` (DATE, Nullable) en lote |

---

## Análisis de Impacto por Capa

### Estado actual de campos relevantes confirmado por exploración

**`MovimientoPolloEngorde` — campos ya existentes:**
```
PesoBruto, PesoTara, PesoNeto, PromedioPesoAve  ← por lote (actualmente duplican el global)
PesoBrutoGlobal, PesoTaraGlobal, PesoNetoGlobal  ← control de ticket (YA EXISTEN en entidad)
```
**Campos faltantes confirmados:**
- `PesoBrutoReal` (decimal?) — fracción proporcional del bruto para este lote
- `PesoTaraReal` (decimal?) — fracción proporcional de la tara para este lote

**`LoteAveEngorde` — campo faltante:**
- `FechaAlistamiento` (DateTime?, nullable)

**`IndicadorEcuadorRequest` — estado actual:**
- `SoloLotesCerrados` (bool) → debe evolucionar a `TipoFiltroLotes: 'cerrados' | 'aves_cero'`

---

## Orden de Ejecución Recomendado

```
R3 (Fecha Alistamiento) → R2 (Prorrateo Ventas) → R1 (Filtro Liquidación)
```

**Justificación:**
- **R3** es puramente aditivo (nueva columna + campo en DTO + control de formulario). Sin efectos colaterales. Más rápido de completar y permite validar el pipeline BD→Backend→Frontend antes de atacar los cambios más complejos.
- **R2** afecta una transacción crítica (PostVentaGranjaDespacho). Requiere migración + cambio de lógica de negocio + UI nueva. Complejidad media-alta. Debe hacerse con la BD ya limpia.
- **R1** no requiere migración de BD (el campo `EstadoOperativoLote` ya existe). Es un cambio de query + UI. Lo dejamos al final para no bloquear los anteriores.

---

## R3 — CAMPO `fecha_alistamiento` EN LOTE ENGORDE

### Base de Datos
```sql
ALTER TABLE lote_ave_engorde
ADD COLUMN fecha_alistamiento DATE NULL;
```
- Tabla: `lote_ave_engorde`
- Migración EF Core: `AddFechaAlistamientoLoteEngorde`
- La columna acepta NULL (campo opcional)

### Backend — Dominio
**Archivo:** `backend/src/ZooSanMarino.Domain/Entities/LoteAveEngorde.cs`
```csharp
public DateTime? FechaAlistamiento { get; set; }
```

### Backend — DTOs
- `CreateLoteAveEngordeDto` → añadir `DateTime? FechaAlistamiento`
- `UpdateLoteAveEngordeDto` → añadir `DateTime? FechaAlistamiento`
- `LoteAveEngordeDetailDto` (record) → añadir propiedad en la lista del record

### Backend — Mapping/Service
- `LoteAveEngordeService.CreateAsync()` y `UpdateAsync()` → mapear `FechaAlistamiento`

### Frontend — Angular
**Archivo:** componente de creación/edición de lote engorde (formulario reactivo)
- `loteForm.addControl('fechaAlistamiento', [null])` (sin validadores, es opcional)
- HTML: nuevo `mat-form-field` con `matDatepicker` en la sección "Identificación del lote"
- Modo edición: `patchValue({ fechaAlistamiento: new Date(lote.fechaAlistamiento) })` si existe

---

## R2 — PRORRATEO DE PESOS EN DESPACHOS MULTI-LOTE

### Análisis de la Entidad Actual
`MovimientoPolloEngorde` ya tiene:
- `PesoBrutoGlobal`, `PesoTaraGlobal`, `PesoNetoGlobal` → campos de control global del ticket ✓
- `PesoBruto`, `PesoTara`, `PesoNeto` → actualmente reciben el valor global duplicado ← BUG

**Campos nuevos a agregar (proporcionales reales):**
- `PesoBrutoReal` (decimal?, nullable) → fracción del bruto asignada a este lote
- `PesoTaraReal` (decimal?, nullable) → fracción de la tara asignada a este lote

> `PesoNeto` (ya existente) se reorienta: pasará a guardar el neto proporcional real.
> `PesoBruto` y `PesoTara` (ya existentes) se limpian: pasarán a 0 o NULL en despachos multi-lote (o se deprecan en favor de los campos `*Real`).

**Decisión arquitectónica:** Para no romper el contrato existente de los DTOs en otros módulos, se agregan `PesoBrutoReal` y `PesoTaraReal` como campos **adicionales**. `PesoNeto` se sigue usando para el neto real del lote (ya no el global).

### Base de Datos
```sql
-- En tabla movimiento_pollo_engorde (verificar nombre exacto en BD)
ALTER TABLE movimiento_pollo_engorde
ADD COLUMN peso_bruto_real NUMERIC(12, 3) NULL,
ADD COLUMN peso_tara_real NUMERIC(12, 3) NULL;
```
- Migración EF Core: `AddPesosRealesMovimientoEngorde`

### LoteRegistroHistoricoUnificado
Verificar si la tabla histórica unificada replica los campos de peso. Si el histórico se construye por trigger o por job que copia desde `movimiento_pollo_engorde`, los campos nuevos se propagarán automáticamente. Si se mapean explícitamente, añadir también en la entidad y en el script SQL de sincronización.

### Backend — Dominio
**Archivo:** `backend/src/ZooSanMarino.Domain/Entities/MovimientoPolloEngorde.cs`
```csharp
public decimal? PesoBrutoReal { get; set; }
public decimal? PesoTaraReal { get; set; }
```

### Backend — DTOs
- `MovimientoPolloEngordeDto` → añadir `PesoBrutoReal`, `PesoTaraReal`
- `CreateMovimientoPolloEngordeDto` → NO añadir (el cálculo es interno al servicio)
- `VentaGranjaDespachoLineaDto` → NO modificar (ya tiene cantidades H/M/X por lote)
- `CreateVentaGranjaDespachoDto` → ya tiene `PesoBruto`, `PesoTara` globales ✓

### Backend — Lógica de Prorrateo (Servicio)
**Archivo:** `MovimientoPolloEngordeService.PostVentaGranjaDespachoAsync()`

Algoritmo a implementar dentro del método (antes del INSERT en BD):

```csharp
// 1. Calcular total aves global
int totalAvesGlobal = lineas.Sum(l => l.CantidadHembras + l.CantidadMachos + l.CantidadMixtas);

// 2. Calcular neto global
decimal pesoNetoGlobal = dto.PesoBruto - dto.PesoTara;

// 3. Calcular peso promedio global
decimal pesoPorAve = totalAvesGlobal > 0 ? pesoNetoGlobal / totalAvesGlobal : 0;

// 4. Calcular prorrateo por lote
decimal sumaNetos = 0;
int indiceLoteMayor = 0;
int maxAves = 0;

for (int i = 0; i < lineas.Count; i++)
{
    var linea = lineas[i];
    int avesLote = linea.CantidadHembras + linea.CantidadMachos + linea.CantidadMixtas;
    decimal factorParticipacion = totalAvesGlobal > 0 ? (decimal)avesLote / totalAvesGlobal : 0;

    movimientos[i].PesoBrutoReal = Math.Round(dto.PesoBruto * factorParticipacion, 3);
    movimientos[i].PesoTaraReal  = Math.Round(dto.PesoTara * factorParticipacion, 3);
    movimientos[i].PesoNeto      = Math.Round(pesoNetoGlobal * factorParticipacion, 3);
    movimientos[i].PromedioPesoAve = Math.Round(pesoPorAve, 3);
    // Campos globales (control de ticket)
    movimientos[i].PesoBrutoGlobal = dto.PesoBruto;
    movimientos[i].PesoTaraGlobal  = dto.PesoTara;
    movimientos[i].PesoNetoGlobal  = pesoNetoGlobal;

    sumaNetos += movimientos[i].PesoNeto ?? 0;
    if (avesLote > maxAves) { maxAves = avesLote; indiceLoteMayor = i; }
}

// 5. Ajuste de residuo al lote con más aves
decimal residuo = pesoNetoGlobal - sumaNetos;
if (Math.Abs(residuo) > 0)
    movimientos[indiceLoteMayor].PesoNeto += residuo;
```

### Backend — Query de Liquidación
Actualizar el método de `IndicadorEcuadorService` que consulta kilos vendidos para que use `SUM(peso_neto_real)` → ya es `SUM(cantidad_kg)` que es `PesoNeto`, el cual después del refactor contendrá el valor real proporcional. **Sin cambio de query necesario** si `PesoNeto` queda como el campo real.

### Frontend — Angular
**Componente:** `modal-movimiento-pollo-engorde.component.ts`

1. **Cálculo en tiempo real (reactivo):**
   - Observar cambios en `pesoBruto` y `pesoTara` con `valueChanges`
   - Calcular y mostrar `pesoNetoGlobal = bruto - tara` y `promedioPesoAve = netoGlobal / totalAvesGlobal`
   - Mostrar en texto informativo gris debajo de los campos de peso

2. **Tabla de prorrateo (preview antes de guardar):**
   - Visible solo cuando `tipoMovimiento = 'Venta'` y hay más de 1 línea
   - Columnas: Galpón/Lote | Aves | % Participación | Peso Bruto | Peso Tara | Peso Neto Real
   - Fila de TOTALES con sumas de verificación
   - Se recalcula en tiempo real con cada cambio en cantidades o pesos

---

## R1 — FILTRO DUAL DE LIQUIDACIÓN ECUADOR

### Análisis del Estado Actual
- `IndicadorEcuadorRequest.SoloLotesCerrados` (bool): actualmente `true` filtra por `aves = 0` O por estado cerrado (verificar lógica exacta en `IndicadorEcuadorService`)
- `LoteAveEngorde.EstadoOperativoLote` (string): valores observados = `"Abierto"` (default), presumiblemente `"Cerrado"` o `"Liquidado"` al cerrar
- `LoteAveEngorde.LiquidadoAt` (DateTime?): fecha en que se liquidó

### No requiere migración de BD
Los campos ya existen. Solo cambia la lógica del query y la UI.

### Backend — DTO
Evolucionar `IndicadorEcuadorRequest`:
```csharp
// Antes:
public bool SoloLotesCerrados { get; init; }

// Después (mantener SoloLotesCerrados para backwards-compat + añadir nuevo):
public string TipoFiltroLotes { get; init; } = "cerrados";
// Valores: "cerrados" (por EstadoOperativoLote) | "aves_cero" (saldo físico = 0)
```

> Alternativa más limpia: reemplazar directamente `SoloLotesCerrados` por `TipoFiltroLotes` si se verifican todos los callers. Dado que el frontend es el único consumidor, es seguro reemplazarlo.

### Backend — Servicio
**Archivo:** `IndicadorEcuadorService.cs`

En el método que filtra lotes para indicadores masivos, cambiar la cláusula WHERE:

```csharp
// Opción A: por estado administrativo
if (request.TipoFiltroLotes == "cerrados")
    query = query.Where(l => l.EstadoOperativoLote == "Cerrado" || l.LiquidadoAt != null);

// Opción B: saldo físico estricto
else if (request.TipoFiltroLotes == "aves_cero")
    query = query.Where(l => /* saldo de aves calculado == 0 */);
```

> Aclaración pendiente: confirmar valores exactos de `EstadoOperativoLote` en producción (puede ser "Cerrado", "Liquidado", "cerrado", etc.) con un SELECT DISTINCT.

### Frontend — Angular
**Componente:** `indicador-ecuador-list.component.ts`

1. **Cambio de modelo:**
   ```typescript
   // Antes:
   soloLotesCerrados: boolean = false;
   
   // Después:
   tipoFiltroLotes: 'cerrados' | 'aves_cero' = 'cerrados';
   ```

2. **HTML — Reemplazar checkbox por mat-radio-group:**
   ```html
   <mat-radio-group [(ngModel)]="tipoFiltroLotes">
     <mat-radio-button value="cerrados">
       Todos los lotes cerrados en el sistema
       <small>Lista lotes cuyo estado sea 'Cerrado/Liquidado', sin importar el saldo físico.</small>
     </mat-radio-button>
     <mat-radio-button value="aves_cero">
       Solo lotes con saldo de aves igual a cero (0)
       <small>Lotes donde el inventario de aves llegó a 0 físicamente.</small>
     </mat-radio-button>
   </mat-radio-group>
   ```

3. **Payload al API:** enviar `tipoFiltroLotes` en lugar de `soloLotesCerrados`

---

## Dependencias y Riesgos

| Riesgo | Afecta | Mitigación |
|--------|--------|-----------|
| `PostVentaGranjaDespacho` es una transacción crítica con registros históricos | R2 | Probar en entorno de desarrollo con datos reales antes de migrar |
| `EstadoOperativoLote` puede tener valores inconsistentes en BD (case sensitivity) | R1 | SELECT DISTINCT antes de implementar el filtro |
| Campos `PesoBrutoReal/PesoTaraReal` en histórico unificado — verificar si la tabla los replica | R2 | Revisar trigger o job de sincronización |
| `SoloLotesCerrados` puede estar hardcodeado en otros endpoints | R1 | Grep en toda la solución antes de renombrar |

---

## Archivos Clave por Requerimiento

### R3 — Fecha Alistamiento
| Capa | Archivo |
|------|---------|
| DB | Migración `AddFechaAlistamientoLoteEngorde` |
| Domain | `ZooSanMarino.Domain/Entities/LoteAveEngorde.cs` |
| DTOs | `Application/DTOs/CreateLoteAveEngordeDto.cs`, `UpdateLoteAveEngordeDto.cs`, `LoteAveEngorde/LoteAveEngordeDetailDto.cs` |
| Service | `Infrastructure/Services/LoteAveEngordeService.cs` (o equivalente) |
| Frontend | Componente formulario lote engorde (buscar en `features/aves-engorde/`) |

### R2 — Prorrateo Ventas
| Capa | Archivo |
|------|---------|
| DB | Migración `AddPesosRealesMovimientoEngorde` |
| Domain | `ZooSanMarino.Domain/Entities/MovimientoPolloEngorde.cs` |
| DTOs | `Application/DTOs/MovimientoPolloEngordeDto.cs` |
| Service | `Infrastructure/Services/MovimientoPolloEngordeService.cs` |
| Frontend | `features/movimientos-pollo-engorde/components/modal-movimiento-pollo-engorde/` |

### R1 — Filtro Liquidación
| Capa | Archivo |
|------|---------|
| DTOs | `Application/DTOs/IndicadorEcuadorRequest.cs` (o donde esté definido) |
| Service | `Infrastructure/Services/IndicadorEcuadorService.cs` |
| Frontend | `features/indicador-ecuador/pages/indicador-ecuador-list/` |
