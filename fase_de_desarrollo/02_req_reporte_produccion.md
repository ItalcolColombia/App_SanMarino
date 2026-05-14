# Plan de Desarrollo — Reporte Técnico de Producción

**Módulo:** Reportes Técnicos · Fase PRODUCCIÓN  
**Fecha de planificación:** 2026-05-06  
**Referencia anterior:** `01_req_reporte_levante.md` (mismos patrones aplicados a producción)

---

## Contexto y Objetivo

El módulo de Reportes Técnicos ya implementó correctamente LEVANTE (semanas 1-25) con:
- Filtro cascada: Granja → Núcleo → Galpón → Lote Base → Sublote
- Endpoint `POST /api/ReporteTecnico/levante/obtener`
- Frontend con Signals, computed cascade, tabs de resultado

El objetivo es replicar el mismo flujo para **PRODUCCIÓN** (etapa de postura):
- Mismo filtro cascada resolviendo hasta `lote_postura_base`
- Lotes de producción (`lote_postura_produccion`) como último nivel de selección
- Nuevo endpoint `POST /api/ReporteTecnicoProduccion/obtener` análogo al de levante
- Frontend integrado en el mismo componente `reporte-tecnico-main`

---

## Análisis Técnico

### Entidades clave

| Entidad | Tabla | FK relevante |
|---------|-------|-------------|
| `LotePosturaBase` | `lote_postura_base` | (raíz) |
| `Lote` | `lotes` | `lote_postura_base_id` → Base |
| `LotePosturaLevante` | `lote_postura_levante` | `lote_id` → Lote |
| `LotePosturaProduccion` | `lote_postura_produccion` | `lote_postura_levante_id` → Levante |
| `SeguimientoDiario` | `seguimiento_diario` | `lote_postura_produccion_id`, `tipo_seguimiento = 'produccion'` |

### Cadena de navegación (4 saltos desde Base)

```
LotePosturaBase (ID raíz)
   └── Lote           [lote.lote_postura_base_id = base.id]
       └── LotePosturaLevante  [lpl.lote_id = lote.lote_id]
           └── LotePosturaProduccion  [lpp.lote_postura_levante_id = lpl.id]
               └── SeguimientoDiario  [sd.lote_postura_produccion_id = lpp.id
                                        AND sd.tipo_seguimiento = 'produccion']
```

### Seguimiento diario de producción

- **Tabla:** `seguimiento_diario` (unificada con levante)
- **Discriminador:** `tipo_seguimiento = 'produccion'`
- **FK:** `lote_postura_produccion_id` (int?)
- **Servicio existente:** `ObtenerSeguimientosProduccionPorLPPAsync(lotePosturaProduccionId)` en `ReporteTecnicoProduccionService`
- **Campos relevantes del seguimiento:** MortalidadHembras/Machos, SelH/M, ConsKgH/M, HuevoTot/Inc/Limpio/Tratado/..., PesoHuevo, PesoH/M, Uniformidad, CV

### Estado del filter-data actual de PRODUCCIÓN

**Problema:** El endpoint `GET /api/ReporteTecnicoProduccion/filter-data` retorna `SeguimientoProduccionFilterDataDto` que incluye `lote_postura_produccion` directamente (sin agrupar por `lote_postura_base`). El frontend normaliza este formato diferente en `_normalizeProduccion()` con `lotePosturaBaseId: null`.

**Solución:** Migrar el endpoint a retornar `LoteReproductoraFilterDataDto` (mismo shape que levante), resolviendo `LotesBase` mediante la cadena de navegación.

### DTOs de resultado ya existentes

- `ReporteTecnicoProduccionDiarioDto` — datos diarios (mortalidad, huevos, consumo, pesos)
- `ReporteTecnicoProduccionSemanalDto` — datos semanales (agrupados)
- `ReporteTecnicoProduccionCompletoDto` — contenedor (LoteInfo + DatosDiarios + DatosSemanales)
- `ReporteTecnicoProduccionCuadroDto` — tabla "Cuadro" con campos GUIA genética

---

## Decisiones de Diseño

### D1 — Tipo de consolidación en PRODUCCIÓN
Un `LotePosturaBase` puede tener múltiples `LotePosturaProduccion` (ej: K345A-H, K345A-M, K345B-H, K345B-M).

**Decisión:** Igual que levante — ofrecer Consolidado (todos los lotes producción del base) vs Por Lote (selección individual). El dropdown de "Lote de Producción" aparece solo cuando se elige "Por Lote".

### D2 — Reutilización del signal `selectedSubloteId`
En el filter service, `selectedSubloteId` ya existe para LEVANTE. Para PRODUCCIÓN se puede reutilizar el mismo slot con semántica diferente por etapa (`selectedSubloteId` en LEVANTE = lote_postura_levante_id; en PRODUCCIÓN = lote_postura_produccion_id).

**Decisión:** Reutilizar `selectedSubloteId` — ambos representan "el subregistro seleccionado dentro del lote base". Agregar computed `sublotesFiltrados` que en PRODUCCIÓN retorne los `LotePosturaProduccion` asociados al `LoteBase` seleccionado.

### D3 — Shape del filter-data de PRODUCCIÓN
**Decisión:** Cambiar el endpoint de producción para que retorne `LoteReproductoraFilterDataDto` (mismo que levante). Esto elimina `_normalizeProduccion()` del frontend y unifica el parsing. `LotesBase` contendrá los `lote_postura_base` accesibles vía la cadena de navegación. `Lotes` contendrá los `lote_postura_produccion` con campo `lotePosturaBaseId` resuelto.

### D4 — `LoteFilterItemDto` para producción
El campo `lotePosturaBaseId` en `LoteFilterItemDto` (backend) debe ser poblado para los lotes de producción. Se resuelve navegando: `LotePosturaProduccion.LotePosturaLevanteId → LotePosturaLevante.LoteId → Lote.LotePosturaBaseId`.

---

## Plan de Implementación

### FASE A — Backend: Filter-data con LoteBase (Prioridad Alta)

#### A1. Actualizar `LoteProduccionFilterDataService`
**Archivo:** `Infrastructure/Services/LoteProduccionFilterDataService.cs`

- Inyectar `ILotePosturaBaseService` y `ZooSanMarinoContext` (o `ILotePosturaLevanteService`)
- Para cada `LotePosturaProduccion`, resolver `LotePosturaBaseId`:
  ```
  lpp.LotePosturaLevanteId → LotePosturaLevante.LoteId → Lote.LotePosturaBaseId
  ```
- Construir `LotesBase: List<LoteBaseFilterItemDto>` con los base únicos accesibles
- Cambiar tipo de retorno: `SeguimientoProduccionFilterDataDto` → `LoteReproductoraFilterDataDto`
- Poblar `Lotes` como `LoteFilterItemDto` con `LotePosturaBaseId` resuelto
  (LoteId = `LotePosturaProduccionId`, LoteNombre = nombre, LotePosturaBaseId = el resuelto)

#### A2. Actualizar `ILoteProduccionFilterDataService`
**Archivo:** `Application/Interfaces/ILoteProduccionFilterDataService.cs`
- Cambiar firma: retornar `Task<LoteReproductoraFilterDataDto>`

#### A3. Actualizar `ReporteTecnicoProduccionController` — endpoint filter-data
**Archivo:** `API/Controllers/ReporteTecnicoProduccionController.cs`
- `GET /filter-data` retorna `LoteReproductoraFilterDataDto`

---

### FASE B — Backend: Nuevo endpoint ObtenerReporteProduccion (Prioridad Alta)

#### B1. Crear `ObtenerReporteProduccionRequestDto`
**Archivo nuevo:** `Application/DTOs/ObtenerReporteProduccionRequestDto.cs`
```csharp
public class ObtenerReporteProduccionRequestDto
{
    public int LotePosturaBaseId { get; set; }
    public int? LotePosturaProduccionId { get; set; }  // null = consolidar todos
    public string FiltroPeriodicidad { get; set; } = "Semanal"; // "Diario" o "Semanal"
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}
```

#### B2. Agregar método a `IReporteTecnicoProduccionService`
**Archivo:** `Application/Interfaces/IReporteTecnicoProduccionService.cs`
```csharp
Task<ReporteTecnicoProduccionCompletoDto> ObtenerReporteProduccionAsync(
    ObtenerReporteProduccionRequestDto request,
    CancellationToken ct = default);
```

#### B3. Implementar `ObtenerReporteProduccionAsync` en `ReporteTecnicoProduccionService`
**Archivo:** `Infrastructure/Services/ReporteTecnicoProduccionService.cs`

Lógica (análoga a `ObtenerReporteLevanteAsync`):
```
1. Validar LotePosturaBase existe y pertenece a la empresa
2. Navegar: Base → Lotes → LotePosturaLevante → LotePosturaProduccion
3. Si LotePosturaProduccionId está presente, filtrar a ese lote específico
4. Para cada LPP, llamar a ObtenerSeguimientosProduccionPorLPPAsync()
5. Aplicar filtro de semanas (etapa de producción = desde semana 26 o semana 1 de producción)
6. Aplicar filtro de fechas (solo si FechaInicio/FechaFin tienen valor)
7. Consolidar o retornar individual según selección
8. Ramificar según FiltroPeriodicidad (Diario / Semanal)
9. Retornar ReporteTecnicoProduccionCompletoDto
```

**Nota sobre semanas:** A diferencia de levante (semanas 1-25 relativas a FechaEncaset),
en producción las semanas son relativas a `FechaInicioProduccion`. No hay límite de 25 semanas.

#### B4. Agregar endpoint `POST /obtener`
**Archivo:** `API/Controllers/ReporteTecnicoProduccionController.cs`
```csharp
[HttpPost("obtener")]
public async Task<IActionResult> ObtenerReporte(
    [FromBody] ObtenerReporteProduccionRequestDto request,
    CancellationToken ct)
```

---

### FASE C — Frontend: Filter Service (Prioridad Alta)

#### C1. Eliminar `_normalizeProduccion()` en el filter service
**Archivo:** `frontend/.../reporte-tecnico-levante-filter.service.ts`

- `_cargarDatos()` para PRODUCCIÓN: ya no necesita normalización; el backend retorna `LevanteFilterData` directamente
- Reemplazar: `http.get<ProduccionFilterDataRaw>(...).pipe(map(raw => _normalizeProduccion(raw)))`
  Por: `http.get<LevanteFilterData>(this.URLS.PRODUCCION)`
- Eliminar interfaz `ProduccionFilterLoteRaw` e `ProduccionFilterDataRaw`

#### C2. Actualizar `sublotesFiltrados` computed para cubrir PRODUCCIÓN
**Archivo:** `frontend/.../reporte-tecnico-levante-filter.service.ts`

```typescript
readonly sublotesFiltrados = computed<LevanteFilterLote[]>(() => {
  const d      = this._data();
  const baseId = this.selectedLoteBaseId();
  const gid    = this.selectedGranjaId();
  const nid    = this.selectedNucleoId();
  const galId  = this.selectedGalponId();
  if (!d || !baseId || !gid) return [];

  // Aplica tanto para LEVANTE (lote_postura_levante) como PRODUCCIÓN (lote_postura_produccion)
  let lotes = d.lotes.filter(l => l.lotePosturaBaseId === baseId && l.granjaId === gid);
  if (nid)   lotes = lotes.filter(l => l.nucleoId === nid);
  if (galId) lotes = lotes.filter(l => l.galponId === galId);

  return lotes.sort((a, b) =>
    a.loteNombre.localeCompare(b.loteNombre, 'es', { numeric: true, sensitivity: 'base' })
  );
});
```

Esto elimina el guard `selectedEtapa() !== 'LEVANTE'` ya que ahora funciona para ambas etapas.

---

### FASE D — Frontend: Componente Principal (Prioridad Media)

#### D1. Nuevo método `_generarReporteProduccion()` en el componente
**Archivo:** `frontend/.../reporte-tecnico-main.component.ts`

```typescript
private _generarReporteProduccion(): void {
  this.loading.set(true);
  this.error = null;

  const request: ObtenerReporteProduccionRequestDto = {
    lotePosturaBaseId: this.filterSvc.selectedLoteBaseId()!,
    lotePosturaProduccionId: this.tipoReporte === 'sublote'
      ? (this.filterSvc.selectedSubloteId() ?? null)
      : null,
    filtroPeriodicidad: this.filterSvc.selectedPeriodicidad(),
    fechaInicio: this.fechaInicio || null,
    fechaFin:    this.fechaFin    || null,
  };

  this.reporteService.obtenerReporteProduccion(request)
    .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
    .subscribe({
      next: (reporte) => {
        this.reporteProduccion.set(reporte);
        this.error = null;
        this.tabProduccionActivo = this.filterSvc.selectedPeriodicidad() === 'Semanal'
          ? 'semanal'
          : 'diario';
      },
      error: (err) => {
        this.error = err.error?.message || 'Error al generar el reporte de producción';
        this.reporteProduccion.set(null);
      }
    });
}
```

#### D2. Agregar `signal` para reporte de producción
```typescript
reporteProduccion = signal<ReporteTecnicoProduccionCompletoDto | null>(null);
tabProduccionActivo: 'semanal' | 'diario' = 'semanal';
```

#### D3. Actualizar `generarReporte()` para despachar a producción
```typescript
generarReporte(): void {
  if (!this.validarFiltros()) return;
  const etapa = this.filterSvc.selectedEtapa();
  if (etapa === 'LEVANTE')    this._generarReporteLevante();
  else                         this._generarReporteProduccion();
}
```

#### D4. Actualizar `limpiarReporte()` para limpiar ambos reportes
```typescript
limpiarReporte(): void {
  this.reporteLevante.set(null);
  this.reporteProduccion.set(null);
  this.error = null;
}
```

#### D5. Actualizar `puedeGenerarReporte()` para producción
```typescript
puedeGenerarReporte(): boolean {
  if (!this.filterSvc.selectedLoteBaseId()) return false;
  const etapa = this.filterSvc.selectedEtapa();
  if (etapa === 'LEVANTE' || etapa === 'PRODUCCION') {
    if (this.tipoReporte === 'sublote' && !this.filterSvc.selectedSubloteId()) return false;
  }
  return true;
}
```

#### D6. Actualizar `exportarExcel()` para producción
- Quitar el guard que bloquea PRODUCCIÓN
- Llamar al endpoint de Excel de producción cuando la etapa sea PRODUCCIÓN

---

### FASE E — Frontend: HTML (Prioridad Media)

#### E1. Label dinámico del dropdown de sublote/lote-produccion
```html
<label class="ux-label" for="sel-sublote">
  {{ filterSvc.selectedEtapa() === 'LEVANTE' ? 'Sublote' : 'Lote de Producción' }}
</label>
```

#### E2. Label del "Tipo de Reporte" visible en PRODUCCIÓN también
```html
@if (filterSvc.selectedEtapa() === 'LEVANTE' || filterSvc.selectedEtapa() === 'PRODUCCION') {
  <!-- Tipo de Reporte: Consolidado / Por Sublote (LEVANTE) / Por Lote (PRODUCCIÓN) -->
  <div class="form-field">
    <span class="ux-label">Tipo de Reporte</span>
    <div class="radio-group">
      <label class="radio-label">
        <input type="radio" name="tipoReporte" value="consolidado"
          [(ngModel)]="tipoReporte" (change)="onTipoReporteChange()">
        Consolidado
      </label>
      <label class="radio-label">
        <input type="radio" name="tipoReporte" value="sublote"
          [(ngModel)]="tipoReporte" (change)="onTipoReporteChange()">
        {{ filterSvc.selectedEtapa() === 'LEVANTE' ? 'Por Sublote' : 'Por Lote' }}
      </label>
    </div>
  </div>
}
```

#### E3. Mostrar dropdown de Lote Producción cuando aplica
- El dropdown de sublote ya condiciona `tipoReporte === 'sublote'`
- Solo cambiar el label según etapa (LEVANTE → "Sublote" / PRODUCCIÓN → "Lote de Producción")

#### E4. Agregar tabs y tabla de resultados de PRODUCCIÓN
```html
@if (reporteProduccion()) {
  <section class="ux-card">
    <!-- Tabs: Semanal | Diario -->
    <!-- Encabezado del reporte -->
    <!-- Tab Semanal: app-tabla-datos-semanales-produccion -->
    <!-- Tab Diario:  app-tabla-datos-diarios-produccion -->
  </section>
}
```

---

### FASE F — Frontend: Service HTTP (Prioridad Media)

#### F1. Agregar `ObtenerReporteProduccionRequestDto` y método en `ReporteTecnicoService`
**Archivo:** `frontend/.../services/reporte-tecnico.service.ts`

```typescript
export interface ObtenerReporteProduccionRequestDto {
  lotePosturaBaseId: number;
  lotePosturaProduccionId?: number | null;
  filtroPeriodicidad: string;
  fechaInicio?: string | null;
  fechaFin?: string | null;
}

obtenerReporteProduccion(
  request: ObtenerReporteProduccionRequestDto
): Observable<ReporteTecnicoProduccionCompletoDto> {
  return this.http.post<ReporteTecnicoProduccionCompletoDto>(
    `${this.baseUrlProduccion}/obtener`,
    request
  );
}
```

---

## Archivos a Modificar

### Backend

| Archivo | Tipo | Cambio |
|---------|------|--------|
| `Application/DTOs/ObtenerReporteProduccionRequestDto.cs` | CREAR | DTO de entrada para el nuevo endpoint |
| `Application/Interfaces/ILoteProduccionFilterDataService.cs` | MODIFICAR | Cambiar tipo retorno a `LoteReproductoraFilterDataDto` |
| `Application/Interfaces/IReporteTecnicoProduccionService.cs` | MODIFICAR | Agregar `ObtenerReporteProduccionAsync()` |
| `Infrastructure/Services/LoteProduccionFilterDataService.cs` | MODIFICAR | Navegar a LoteBase, retornar `LoteReproductoraFilterDataDto` |
| `Infrastructure/Services/ReporteTecnicoProduccionService.cs` | MODIFICAR | Implementar `ObtenerReporteProduccionAsync()` |
| `API/Controllers/ReporteTecnicoProduccionController.cs` | MODIFICAR | Añadir `POST /obtener`, actualizar `GET /filter-data` |

### Frontend

| Archivo | Tipo | Cambio |
|---------|------|--------|
| `services/reporte-tecnico-levante-filter.service.ts` | MODIFICAR | Eliminar normalización PRODUCCIÓN, actualizar `sublotesFiltrados` |
| `services/reporte-tecnico.service.ts` | MODIFICAR | Agregar DTO + método `obtenerReporteProduccion()` |
| `reporte-tecnico-main.component.ts` | MODIFICAR | Señal producción, `_generarReporteProduccion()`, despacho |
| `reporte-tecnico-main.component.html` | MODIFICAR | Labels dinámicos, tabs producción, tabla resultados |

---

## Validaciones Críticas

- **Semanas en producción**: Las semanas se calculan desde `FechaInicioProduccion` (no `FechaEncaset`). Si `FechaInicioProduccion` es null, usar el primer registro de seguimiento como referencia (patrón ya existente en `ReporteTecnicoProduccionService`).
- **Sin límite de semanas**: A diferencia de levante (≤ 25 semanas), producción no tiene límite fijo.
- **Null safety**: `LotePosturaProduccion.LotePosturaLevanteId` puede ser null (lotes legacy). Estos no se vincularán a ningún `LotePosturaBase` y quedarán fuera del filtro por base.
- **Aves iniciales**: En producción usar `AvesHInicial` / `AvesMInicial` (o `HembrasInicialesProd`/`MachosInicialesProd`) como denominador de porcentajes.
- **FechaInicio/FechaFin null**: Si no se envían, retornar todos los registros (misma lógica que levante).

---

## Orden de Ejecución Recomendado

1. **A1 → A2 → A3**: Arreglar filter-data backend → probar con curl
2. **C1 → C2**: Actualizar frontend filter service → verificar dropdown LoteBase en PRODUCCIÓN
3. **B1 → B2 → B3 → B4**: Implementar endpoint `/obtener` → probar con Swagger
4. **F1**: Agregar método al service HTTP frontend
5. **D1-D5**: Conectar en el componente principal
6. **E1-E4**: Actualizar HTML con tabs y resultados
7. Build + prueba E2E

---

## Notas de Referencia

- Seguimiento diario de producción: `sd.tipo_seguimiento = 'produccion'` en tabla `seguimiento_diario`
- Servicio que lee ese seguimiento: `ReporteTecnicoProduccionService.ObtenerSeguimientosProduccionPorLPPAsync()`
- Componentes tabla ya existentes: `TablaLevanteSemanalesHembrasComponent`, `TablaDatosDiariosProduccionComponent`, `TablaDatosSemanalesProduccionComponent`
- Espejo de huevos: `EspejoHuevoProduccion` (histórico/dinámico) — no se consulta en el reporte técnico base
