# Plan de Desarrollo — Reportes con TABs y Diseño de Hojas

**Módulo:** Reportes Técnicos · Sistema de TABs y Hojas de Reporte  
**Fecha de planificación:** 2026-05-07  
**Contexto anterior:** Fase 1-3 completadas (backend LEVANTE, PRODUCCIÓN; frontend integrado)  
**Referencia de datos:** `diccionario_datos_levante.md` + especificación en `ESPECIFICACION_COMPLETA_PARA_CLAUDE_CODE.docx`

**⚠️ IMPORTANTE:** Esta Fase 4 es **SOLO PARA ETAPA PRODUCCIÓN**  
- Cuando `selectedEtapa() === 'LEVANTE'` → Se usa el reporte actual de levante (ya implementado)
- Cuando `selectedEtapa() === 'PRODUCCION'` → Se usa este nuevo sistema de TABs de Fase 4

---

## Contexto y Objetivo

Las Fases 1-3 implementaron los endpoints backend y el sistema de filtros frontend para LEVANTE y PRODUCCIÓN. 

**LEVANTE** (Fases 1-3): Ya tiene su propio componente de reporte con tabs para datos semanales vs diarios.

**PRODUCCIÓN** (Fase 4 — ESTA): Se necesita un nuevo sistema de TABs que muestre múltiples hojas de reporte (como en Excel):

1. **Sistema de TABs dinámico** que muestre múltiples hojas de reporte (como en Excel) — **SOLO EN PRODUCCIÓN**
2. **Reportes por tipo:**
   - Reporte Diario por Galpón (una tabla por galpón)
   - Reporte Semanal por Galpón (una tabla por galpón)
   - Reporte Diario General (consolidado de todos los galpones)
   - Reporte Semanal General (consolidado de todos los galpones)
3. **Comparativa vs. STANDARD genético:** Cada reporte mostrará datos reales vs. valores guía
4. **Formateo visual estilo Excel:** Cabeceras pegajosas, semáforos de desviación, colores por métrica

---

## Análisis Técnico

### Estructura de datos actual

En Fase 3 se implementó el endpoint `POST /api/ReporteTecnicoProduccion/obtener` que retorna:

```csharp
public class ReporteTecnicoProduccionCompletoDto
{
    public LoteInfo LoteInfo { get; set; }
    public List<ReporteTecnicoProduccionDiarioDto> DatosDiarios { get; set; }
    public List<ReporteTecnicoProduccionSemanalDto> DatosSemanales { get; set; }
}
```

Donde:
- `DatosDiarios`: Lista de registros diarios con campos individuales
- `DatosSemanales`: Lista de semanas con agregaciones (sum, avg)

### Necesidad: Desglose por Galpón

El problema actual: `DatosDiarios` y `DatosSemanales` integran TODOS los galpones en una sola lista.

**Solución:** Agregación en backend:
- Si es "Consolidado": retornar datos ya agregados (suma de galpones)
- Si es "Por Lote": retornar datos desglosados por `lote_postura_produccion_id`

Esto permite en frontend renderizar una tabla **por galpón** (TAB por galpón).

---

## Decisiones de Diseño

### D1 — Estructura de TABs

```
┌────────────────────────────────────────────┐
│ [Lote K345A] → Periodo: [2026-W01 a 2026-W10]
├────────────────────────────────────────────┤
│ ◆ Diario/Galpón  │ ◇ Semanal/Galpón       │
│ ◆ Diario General │ ◆ Semanal General      │
├────────────────────────────────────────────┤
│ [TABLA DE DATOS]                           │
│ Fecha | Galpón | Mortalidad H | ... | HTAA│
│ ------|--------|---(GUIA)-----|...|-------|
└────────────────────────────────────────────┘
```

Primero: TAB por "Periodicidad" (Diario vs Semanal)
Luego dentro: TAB por "Ámbito" (Por Galpón vs General) O usar un selector dropdown

### D2 — Componentes a crear

1. **ReportesTabsComponent** (padre)
   - Gestiona el estado de TABs seleccionados
   - Renderiza selector de periodicidad
   - Renderiza 2-4 sub-tabs (Por Galpón, General, etc.)

2. **ReporteDiarioGalponComponent** (hijo)
   - Recibe `ReporteDiarioGalponDto` como input
   - Tabla: Fecha | Mortalidad H/M | Consumo H/M | Huevos Tot/Inc | % Produc | Peso | Uniformidad | CV
   - Columnas GUIA al lado (Real → Guía → Diferencia)

3. **ReporteSemanalGalponComponent** (hijo)
   - Tabla: Semana | Período | Datos agregados semanales

4. **ReporteGeneralDiarioComponent** (hijo)
   - Tabla: Galpones en filas + métricas en columnas
   - Fila final: TOTAL

5. **ReporteGeneralSemanalComponent** (hijo)
   - Similar a diario pero con datos semanales

### D3 — Datos desglosados por Galpón

Crear DTOs nuevos:

```csharp
public class ReporteDiarioGalponDto
{
    public int GalponId { get; set; }
    public string GalponNombre { get; set; }
    public DateTime Fecha { get; set; }
    // Datos reales
    public int MortalidadHembras { get; set; }
    public int MortalidadMachos { get; set; }
    public double ConsKgH { get; set; }
    public double ConsKgM { get; set; }
    public int HuevoTot { get; set; }
    public int HuevoInc { get; set; }
    public double PesoHuevo { get; set; }
    public double? Uniformidad { get; set; }
    public double? CoeficienteVariacion { get; set; }
    // Cálculos
    public double PorcentajeMortalidad { get; set; }
    public double PorcentajeProduccion { get; set; }
    // GUIA (tabla STANDARD)
    public double? PesoHuevoGuia { get; set; }
    public double? HtaaGuia { get; set; }
    public double? UniformidadGuia { get; set; }
}
```

### D4 — Lógica en backend para desglose

En `ReporteTecnicoProduccionService.ObtenerReporteProduccionAsync()`:

**Antes:** Retornar `ReporteTecnicoProduccionCompletoDto` con datos agregados o mixtos

**Después:** Retornar estructura desglosada:

```csharp
public class ReporteTecnicoProduccionTabsDto
{
    public LoteInfo LoteInfo { get; set; }
    public List<ReporteDiarioGalponDto> DiariosGalpon { get; set; }  // Desglosado
    public List<ReporteSemanalGalponDto> SemanalesGalpon { get; set; }
    public ReportGeneralDiarioDto DiariosGeneral { get; set; }  // Consolidado
    public ReporteGeneralSemanalDto SemanalesGeneral { get; set; }
}
```

---

## Plan de Implementación

### FASE G — Backend: DTOs y Lógica de Desglose (Prioridad Alta)

#### G1. Crear nuevos DTOs
**Archivos nuevos:**
- `Application/DTOs/ReporteDiarioGalponDto.cs`
- `Application/DTOs/ReporteSemanalGalponDto.cs`
- `Application/DTOs/ReporteGeneralDiarioDto.cs`
- `Application/DTOs/ReporteGeneralSemanalDto.cs`
- `Application/DTOs/ReporteTecnicoProduccionTabsDto.cs`

**Estructura base de cada DTO:**

```csharp
public class ReporteDiarioGalponDto
{
    public int GalponId { get; set; }
    public string GalponNombre { get; set; }
    public int LotePosturaProduccionId { get; set; }
    public DateTime Fecha { get; set; }
    public int Semana { get; set; }
    
    // ===== DATOS REALES — MORTALIDAD Y SELECCIÓN =====
    [JsonPropertyName("mortalidad_hembras")]
    public int MortalidadHembras { get; set; }
    
    [JsonPropertyName("mortalidad_machos")]
    public int MortalidadMachos { get; set; }
    
    [JsonPropertyName("seleccion_h")]
    public int SelH { get; set; }
    
    [JsonPropertyName("seleccion_m")]
    public int SelM { get; set; }
    
    // ===== DATOS REALES — CONSUMO =====
    [JsonPropertyName("cons_kg_h")]
    public double ConsKgH { get; set; }
    
    [JsonPropertyName("cons_kg_m")]
    public double ConsKgM { get; set; }
    
    [JsonPropertyName("consumo_agua_diario")]
    public double? ConsumoAguaDiario { get; set; }
    
    // ===== DATOS REALES — HUEVOS =====
    [JsonPropertyName("huevo_tot")]
    public int HuevoTot { get; set; }
    
    [JsonPropertyName("huevo_inc")]
    public int HuevoInc { get; set; }
    
    [JsonPropertyName("huevos_acumulados")]
    public int HuevosAcumulados { get; set; }
    
    // ===== CLASIFICACIÓN DETALLADA DE HUEVOS =====
    [JsonPropertyName("huevo_limpio")]
    public int HuevoLimpio { get; set; }
    
    [JsonPropertyName("huevo_tratado")]
    public int HuevoTratado { get; set; }
    
    [JsonPropertyName("huevo_sucio")]
    public int HuevoSucio { get; set; }
    
    [JsonPropertyName("huevo_deforme")]
    public int HuevoDeforme { get; set; }
    
    [JsonPropertyName("huevo_blanco")]
    public int HuevoBlanco { get; set; }
    
    [JsonPropertyName("huevo_doble_yema")]
    public int HuevoDobleYema { get; set; }
    
    [JsonPropertyName("huevo_piso")]
    public int HuevoPiso { get; set; }
    
    [JsonPropertyName("huevo_pequeno")]
    public int HuevoPequeno { get; set; }
    
    [JsonPropertyName("huevo_roto")]
    public int HuevoRoto { get; set; }
    
    [JsonPropertyName("huevo_desecho")]
    public int HuevoDesecho { get; set; }
    
    [JsonPropertyName("huevo_otro")]
    public int HuevoOtro { get; set; }
    
    // ===== DATOS REALES — PESO Y CARACTERÍSTICAS =====
    [JsonPropertyName("peso_huevo")]
    public double PesoHuevo { get; set; }
    
    [JsonPropertyName("uniformidad")]
    public double? Uniformidad { get; set; }
    
    [JsonPropertyName("coeficiente_variacion")]
    public double? CoeficienteVariacion { get; set; }
    
    // ===== INCUBACIÓN =====
    [JsonPropertyName("huevos_incubadora")]
    public int? HuevosIncubadora { get; set; }
    
    [JsonPropertyName("huevos_otras_incubadoras")]
    public int? HuevosOtrasIncubadoras { get; set; }
    
    // ===== CÁLCULOS Y RATIOS =====
    [JsonPropertyName("porcentaje_mortalidad")]
    public double? PorcentajeMortalidad { get; set; }
    
    [JsonPropertyName("porcentaje_produccion")]
    public double? PorcentajeProduccion { get; set; }
    
    [JsonPropertyName("porcentaje_huevo_apto")]
    public double? PorcentajeHuevoApto { get; set; }  // (HuevoInc / HuevoTot) * 100
    
    [JsonPropertyName("consumo_total_kg")]
    public double? ConsumoTotalKg { get; set; }
    
    [JsonPropertyName("consumo_por_ave_g")]
    public double? ConsumoPorAveG { get; set; }
    
    [JsonPropertyName("masa_huevo")]
    public double? MasaHuevo { get; set; }
    
    [JsonPropertyName("htaa")]
    public double? Htaa { get; set; }
    
    [JsonPropertyName("relacion_alimento_masa")]
    public double? RelacionAlimentoMasa { get; set; }
    
    [JsonPropertyName("porcentaje_machos_hembras")]
    public double? PorcentajeMachosHembras { get; set; }
    
    [JsonPropertyName("densidad_aves_m2")]
    public double? DensidadAvesM2 { get; set; }
    
    // ===== VALORES GUÍA (TABLA STANDARD — GENÉTICA) =====
    [JsonPropertyName("peso_huevo_guia")]
    public double? PesoHuevoGuia { get; set; }
    
    [JsonPropertyName("htaa_guia")]
    public double? HtaaGuia { get; set; }
    
    [JsonPropertyName("uniformidad_guia")]
    public double? UniformidadGuia { get; set; }
    
    [JsonPropertyName("produccion_guia")]
    public double? ProduccionGuia { get; set; }
    
    [JsonPropertyName("masa_huevo_guia")]
    public double? MasaHuevoGuia { get; set; }
    
    // ===== DESVIACIONES (Real - Guía) =====
    [JsonPropertyName("diferencia_peso")]
    public double? DiferenciaPeso { get; set; }
    
    [JsonPropertyName("diferencia_htaa")]
    public double? DiferenciaHtaa { get; set; }
    
    [JsonPropertyName("diferencia_uniformidad")]
    public double? DiferenciaUniformidad { get; set; }
    
    [JsonPropertyName("diferencia_produccion")]
    public double? DiferenciaProduccion { get; set; }
    
    [JsonPropertyName("diferencia_masa_huevo")]
    public double? DiferenciaMasaHuevo { get; set; }
    
    [JsonPropertyName("observaciones")]
    public string? Observaciones { get; set; }
}
```

#### G2. Actualizar `ReporteTecnicoProduccionService`
**Archivo:** `Infrastructure/Services/ReporteTecnicoProduccionService.cs`

Método nuevo: `ObtenerReporteProduccionDesglosasoAsync()`
- Llamar a `ObtenerSeguimientosProduccionPorLPPAsync()` para cada LPP
- Agrupar resultados por `GalponId` (de metadata o relación)
- Mapear a `ReporteDiarioGalponDto` con cálculos
- Unir con tabla `ProduccionAvicolaRaw` para campos GUIA
- Retornar `ReporteTecnicoProduccionTabsDto` con datos desglosados

#### G3. Crear endpoint `POST /obtener-tabs`
**Archivo:** `API/Controllers/ReporteTecnicoProduccionController.cs`

```csharp
[HttpPost("obtener-tabs")]
public async Task<IActionResult> ObtenerReporteTabs(
    [FromBody] ObtenerReporteProduccionRequestDto request,
    CancellationToken ct)
{
    var reporte = await _reportService.ObtenerReporteProduccionTabsAsync(request, ct);
    return Ok(reporte);
}
```

---

### FASE H — Frontend: Componentes de TABs (Prioridad Media)

#### H1. Crear `ReportesTabsComponent`
**Archivo nuevo:** `frontend/.../components/reportes-tabs/reportes-tabs.component.ts`

```typescript
@Component({
  selector: 'app-reportes-tabs',
  templateUrl: './reportes-tabs.component.html',
  styleUrls: ['./reportes-tabs.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, MatTabsModule, ReporteDiarioGalponComponent, ...]
})
export class ReportesTabsComponent implements OnInit {
  @Input() reporte!: ReporteTecnicoProduccionTabsDto;
  @Input() periodicidad!: 'Diario' | 'Semanal';
  
  tabActivo = signal<'galpon' | 'general'>('galpon');
  
  get galpones(): string[] {
    // Extraer lista única de galpones del reporte
    return [...new Set(
      this.reporte.diariosGalpon.map(r => r.galponNombre)
    )];
  }
  
  onTabChange(tab: 'galpon' | 'general'): void {
    this.tabActivo.set(tab);
  }
}
```

#### H2. Crear `ReporteDiarioGalponComponent`
**Archivo nuevo:** `frontend/.../components/reporte-diario-galpon/reporte-diario-galpon.component.ts`

```typescript
@Component({
  selector: 'app-reporte-diario-galpon',
  templateUrl: './reporte-diario-galpon.component.html',
  styleUrls: ['./reporte-diario-galpon.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, MatTableModule, ...]
})
export class ReporteDiarioGalponComponent {
  @Input() datos!: ReporteDiarioGalponDto[];
  @Input() galponNombre!: string;
  
  // Columnas a mostrar — Orden: Básicas → Huevos → Clasificación → Incubación → Cálculos → Guía → Desviaciones
  displayedColumns = [
    'fecha',
    'semana',
    // Mortalidad y Selección
    'mortalidad_hembras',
    'mortalidad_machos',
    'porcentaje_mortalidad',
    'seleccion_h',
    'seleccion_m',
    // Consumo
    'cons_kg_h',
    'cons_kg_m',
    'consumo_total_kg',
    'consumo_por_ave_g',
    'consumo_agua_diario',
    // Producción de Huevos
    'huevo_tot',
    'huevo_inc',
    'huevos_acumulados',
    'porcentaje_produccion',
    'porcentaje_huevo_apto',
    // Clasificación de Huevos (11 campos)
    'huevo_limpio',
    'huevo_tratado',
    'huevo_sucio',
    'huevo_deforme',
    'huevo_blanco',
    'huevo_doble_yema',
    'huevo_piso',
    'huevo_pequeno',
    'huevo_roto',
    'huevo_desecho',
    'huevo_otro',
    // Incubación
    'huevos_incubadora',
    'huevos_otras_incubadoras',
    // Peso y Características
    'peso_huevo',
    'uniformidad',
    'coeficiente_variacion',
    // Ratios Avanzados
    'masa_huevo',
    'htaa',
    'relacion_alimento_masa',
    'porcentaje_machos_hembras',
    'densidad_aves_m2',
    // Guía
    'peso_huevo_guia',
    'htaa_guia',
    'uniformidad_guia',
    'produccion_guia',
    'masa_huevo_guia',
    // Desviaciones
    'diferencia_peso',
    'diferencia_htaa',
    'diferencia_uniformidad',
    'diferencia_produccion',
    'diferencia_masa_huevo',
    // Notas
    'observaciones'
  ];
  
  /**
   * Calcula color de semáforo basado en desviación vs guía
   * 🟢 Verde: Dentro de rango (-5% a +5%)
   * 🟡 Amarillo: Fuera de rango (±5% a ±15%)
   * 🔴 Rojo: Crítico (> ±15% o anomalía)
   */
  semaforo(valor: number | null, guia: number | null, tolerancia: number = 5): 'success' | 'warning' | 'danger' {
    if (valor === null || guia === null) return 'success';
    const desvPercent = ((valor - guia) / guia) * 100;
    if (Math.abs(desvPercent) <= tolerancia) return 'success';
    if (Math.abs(desvPercent) <= 15) return 'warning';
    return 'danger';
  }
  
  /**
   * Obtiene el ícono de semáforo para la celda
   */
  getSemaforoIcon(estado: 'success' | 'warning' | 'danger'): string {
    return {
      success: '🟢',
      warning: '🟡',
      danger: '🔴'
    }[estado];
  }
}
```

#### H3. Crear templates HTML
**Archivos nuevos:**
- `reportes-tabs.component.html`
- `reporte-diario-galpon.component.html`
- `reporte-semanal-galpon.component.html`
- `reporte-general-diario.component.html`
- `reporte-general-semanal.component.html`

---

### FASE I — Frontend: Integración con Componente Principal (Prioridad Media)

#### I1. Actualizar `reporte-tecnico-main.component.ts`
**Archivo:** `frontend/.../reporte-tecnico-main.component.ts`

```typescript
// LEVANTE — ya existe
reporteLevante = signal<ReporteTecnicoLevanteCompletoDto | null>(null);
tabLevanteActivo: 'semanal' | 'diario' = 'semanal';

// PRODUCCIÓN — agregar SOLO para TABs de producción
reporteProduccionTabs = signal<ReporteTecnicoProduccionTabsDto | null>(null);
tabProduccionActivo: 'galpon' | 'general' = 'galpon';
```

#### I2. Actualizar `_generarReporteProduccion()`
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

  // ⚠️ IMPORTANTE: Llamar al endpoint de TABS (Fase 4) en lugar de al anterior
  this.reporteService.obtenerReporteProduccionTabs(request)
    .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
    .subscribe({
      next: (reporte) => {
        this.reporteProduccionTabs.set(reporte);
        this.error = null;
      },
      error: (err) => {
        this.error = err.error?.message || 'Error al generar el reporte de producción';
        this.reporteProduccionTabs.set(null);
      }
    });
}
```

#### I3. Actualizar HTML — **LÓGICA CONDICIONAL POR ETAPA**
**Archivo:** `reporte-tecnico-main.component.html`

```html
<!-- LEVANTE: Usa el componente existente de levante -->
@if (filterSvc.selectedEtapa() === 'LEVANTE' && reporteLevante()) {
  <section class="ux-card">
    <!-- Tabs LEVANTE: Real vs Guía, etc. -->
    <!-- (mantener como está en fases 1-3) -->
  </section>
}

<!-- PRODUCCIÓN: Usa el nuevo componente de TABs (Fase 4) -->
@if (filterSvc.selectedEtapa() === 'PRODUCCION' && reporteProduccionTabs()) {
  <app-reportes-tabs
    [reporte]="reporteProduccionTabs()!"
    [periodicidad]="filterSvc.selectedPeriodicidad()">
  </app-reportes-tabs>
}
```

---

### FASE J — Frontend: Service HTTP (Prioridad Media)

#### J1. Agregar método en `ReporteTecnicoService`
**Archivo:** `frontend/.../services/reporte-tecnico.service.ts`

```typescript
obtenerReporteProduccionTabs(
  request: ObtenerReporteProduccionRequestDto
): Observable<ReporteTecnicoProduccionTabsDto> {
  return this.http.post<ReporteTecnicoProduccionTabsDto>(
    `${this.baseUrlProduccion}/obtener-tabs`,
    request
  );
}
```

---

## Mapeo Detallado: Excel → DTO → Frontend

### Clasificación de Campos por Fuente

#### CAMPOS BÁSICOS (De tabla `produccion_diaria`)
| Excel | DTO | Tipo | Cálculo |
|-------|-----|------|---------|
| Fecha | Fecha | DateTime | Directo |
| Semana | Semana | int | Directo |
| Mortalidad día (H/M) | MortalidadHembras/Machos | int | Directo |
| Selección día (H/M) | SelH/SelM | int | Directo |
| Consumo (Kg) H/M | ConsKgH/ConsKgM | double | Directo |
| Consumo de agua | ConsumoAguaDiario | double | Directo |

#### CAMPOS DE HUEVOS — CONTEOS (De tabla `produccion_diaria`)
| Excel | DTO | Tipo | Notas |
|-------|-----|------|-------|
| Producción Huevos Totales | HuevoTot | int | Directo |
| Huevos Incubadora | HuevoInc | int | Directo |
| Huevos Acumulados | HuevosAcumulados | int | Sum acumulada desde inicio lote |

#### CAMPOS DE HUEVOS — CLASIFICACIÓN (De tabla `produccion_diaria`)
| Categoría | DTOs | Tipo | Fuente |
|-----------|------|------|--------|
| **Clasificación Limpio/Defectuoso** | HuevoLimpio, HuevoTratado, HuevoSucio, HuevoDeforme | int | Columnas en produccion_diaria |
| **Defectos Específicos** | HuevoBlanco, HuevoDobleYema, HuevoPiso, HuevoPequeno, HuevoRoto | int | Subcolumnas de clasificación |
| **Rechazo** | HuevoDesecho, HuevoOtro | int | Categorías finales |
| **Incubación Externa** | HuevosIncubadora, HuevosOtrasIncubadoras | int | Datos de venta/distribución |

#### CAMPOS CALCULADOS (En Backend)
| DTO | Fórmula | Descripción |
|-----|---------|-------------|
| PorcentajeMortalidad | (MortalidadH + MortalidadM) / TotalAves * 100 | % diario |
| PorcentajeProduccion | (HuevoTot / TotalAves) * 100 | % de postura |
| PorcentajeHuevoApto | (HuevoInc / HuevoTot) * 100 | Huevos incubables / Totales |
| ConsumoTotalKg | ConsKgH + ConsKgM | Suma consumos |
| ConsumoPorAveG | (ConsumoTotalKg * 1000) / TotalAves | Consumo individual |
| MasaHuevo | (HuevoTot * PesoHuevo) / 1000 | Peso total de huevos (Kg) |
| Htaa | (HuevoInc * PesoHuevo) / 100 | Huevo Total Aprovechable Ave |
| RelacionAlimentoMasa | ConsumoTotalKg / MasaHuevo | Alimento / Masa producida |
| DensidadAvesM2 | TotalAves / Area | Aves por metro cuadrado |

#### CAMPOS GUÍA (De tabla `ProduccionAvicolaRaw` — STANDARD)
Join con tabla STANDARD por: Raza + Año + Semana desde encasetamiento

| DTO Guía | Descripción |
|----------|-------------|
| PesoHuevoGuia | Peso esperado de huevo (g) |
| HtaaGuia | HTAA esperado |
| UniformidadGuia | % uniformidad esperada |
| ProduccionGuia | % producción esperada |
| MasaHuevoGuia | Masa esperada |

#### CAMPOS DE DESVIACIÓN (Cálculo: Real - Guía)
| DTO | Fórmula | Cómo se usa |
|-----|---------|-----------|
| DiferenciaPeso | PesoHuevo - PesoHuevoGuia | Varianza del peso |
| DiferenciaHtaa | Htaa - HtaaGuia | Varianza de HTAA |
| DiferenciaUniformidad | Uniformidad - UniformidadGuia | Varianza de uniformidad |
| DiferenciaProduccion | PorcentajeProduccion - ProduccionGuia | Varianza de postura |
| DiferenciaMasaHuevo | MasaHuevo - MasaHuevoGuia | Varianza de masa |

---

## Lógica de Extracción desde Base de Datos

### 1. **Obtener datos de `produccion_diaria`**

```sql
SELECT 
    pd.fecha,
    pd.semana,
    pd.mortalidad_hembras,
    pd.mortalidad_machos,
    pd.seleccion_h,
    pd.seleccion_m,
    pd.consumo_kg_h,
    pd.consumo_kg_m,
    pd.consumo_agua_diario,
    pd.huevo_tot,
    pd.huevo_inc,
    pd.huevos_acumulados,
    pd.huevo_limpio,
    pd.huevo_tratado,
    pd.huevo_sucio,
    pd.huevo_deforme,
    pd.huevo_blanco,
    pd.huevo_doble_yema,
    pd.huevo_piso,
    pd.huevo_pequeno,
    pd.huevo_roto,
    pd.huevo_desecho,
    pd.huevo_otro,
    pd.huevos_incubadora,
    pd.huevos_otras_incubadoras,
    pd.peso_huevo,
    pd.uniformidad,
    pd.coeficiente_variacion,
    pd.relacion_alimento_masa,
    pd.porcentaje_machos_hembras,
    pd.densidad_aves_m2
FROM produccion_diaria pd
WHERE pd.lote_postura_produccion_id = @loteId
ORDER BY pd.fecha
```

### 2. **JOIN con tabla STANDARD para GUÍA**

```sql
LEFT JOIN ProduccionAvicolaRaw standard
ON standard.raza_id = pd.raza_id
   AND standard.año = YEAR(pd.fecha)
   AND standard.semana_desde_encaset = pd.semana_desde_encaset
```

### 3. **Cálculos en Backend (C#)**

```csharp
var dto = new ReporteDiarioGalponDto
{
    // Directos
    Fecha = pd.fecha,
    MortalidadHembras = pd.mortalidad_hembras,
    HuevoTot = pd.huevo_tot,
    // ... más campos directos
    
    // Calculados
    PorcentajeMortalidad = ((pd.mortalidad_hembras + pd.mortalidad_machos) / totalAves) * 100,
    ConsumoTotalKg = pd.consumo_kg_h + pd.consumo_kg_m,
    MasaHuevo = (pd.huevo_tot * pd.peso_huevo) / 1000,
    Htaa = (pd.huevo_inc * pd.peso_huevo) / 100,
    RelacionAlimentoMasa = (pd.consumo_kg_h + pd.consumo_kg_m) / ((pd.huevo_tot * pd.peso_huevo) / 1000),
    
    // Desde STANDARD (puede ser null)
    PesoHuevoGuia = standard?.peso_huevo,
    HtaaGuia = standard?.htaa,
    
    // Desviaciones
    DiferenciaPeso = pd.peso_huevo - standard?.peso_huevo,
    DiferenciaHtaa = htaa_calc - standard?.htaa
};
```

---

## Archivos a Crear/Modificar

### Backend — Crear (7 archivos)

| Archivo | Tipo | Descripción |
|---------|------|------------|
| `Application/DTOs/ReporteDiarioGalponDto.cs` | CREAR | DTO para reporte diario de un galpón |
| `Application/DTOs/ReporteSemanalGalponDto.cs` | CREAR | DTO para reporte semanal de un galpón |
| `Application/DTOs/ReporteGeneralDiarioDto.cs` | CREAR | DTO consolidado (todos los galpones) diario |
| `Application/DTOs/ReporteGeneralSemanalDto.cs` | CREAR | DTO consolidado semanal |
| `Application/DTOs/ReporteTecnicoProduccionTabsDto.cs` | CREAR | DTO contenedor con estructura de TABs |
| `Infrastructure/Services/ReporteTecnicoProduccionService.cs` | MODIFICAR | Agregar `ObtenerReporteProduccionTabsAsync()` |
| `API/Controllers/ReporteTecnicoProduccionController.cs` | MODIFICAR | Agregar endpoint `POST /obtener-tabs` |

### Backend — Modificar (1 archivo)

| Archivo | Cambio |
|---------|--------|
| `Application/Interfaces/IReporteTecnicoProduccionService.cs` | Agregar firma `ObtenerReporteProduccionTabsAsync()` |

### Frontend — Crear (10 archivos)

| Archivo | Tipo | Descripción |
|---------|------|------------|
| `reportes-tabs/reportes-tabs.component.ts` | CREAR | Componente padre de TABs |
| `reportes-tabs/reportes-tabs.component.html` | CREAR | Template TABs |
| `reportes-tabs/reportes-tabs.component.scss` | CREAR | Estilos TABs |
| `reporte-diario-galpon/reporte-diario-galpon.component.ts` | CREAR | Tabla diaria por galpón |
| `reporte-diario-galpon/reporte-diario-galpon.component.html` | CREAR | Template tabla diaria |
| `reporte-diario-galpon/reporte-diario-galpon.component.scss` | CREAR | Estilos tabla |
| `reporte-semanal-galpon/reporte-semanal-galpon.component.ts` | CREAR | Tabla semanal por galpón |
| `reporte-general-diario/reporte-general-diario.component.ts` | CREAR | Tabla consolidada diaria |
| `reporte-general-semanal/reporte-general-semanal.component.ts` | CREAR | Tabla consolidada semanal |
| `shared/directives/semaforo.directive.ts` | CREAR | Directiva para colorear celdas |

### Frontend — Modificar (3 archivos)

| Archivo | Cambio |
|---------|--------|
| `services/reporte-tecnico.service.ts` | Agregar método `obtenerReporteProduccionTabs()` |
| `reporte-tecnico-main.component.ts` | Agregar signal `reporteProduccionTabs` + actualizar `_generarReporteProduccion()` (solo PRODUCCIÓN) |
| `reporte-tecnico-main.component.html` | Agregar bloque condicional `@if (etapa === 'PRODUCCION')` con `<app-reportes-tabs>` (SIN afectar HTML de LEVANTE) |

---

## Estructura de Tabla — Ejemplo Reporte Diario

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│ REPORTE DIARIO — Galpón 14 · K345A (LEVANTE)                                       │
├─────────────────────────────────────────────────────────────────────────────────────┤
│ Fecha      │ Mor.H │ Mor.M │ %Mort │ C.H │ C.M │ Huevo.Tot │ Huevo.Inc │ %Prod │...│
│ (sticky)   │ Real  │ Real  │ Calc  │Real │Real │ Real      │ Real      │ Calc  │   │
│            │ Guía  │ Guía  │ Dif   │Guía │Guía │ Guía      │ Guía      │ Dif   │   │
├─────────────────────────────────────────────────────────────────────────────────────┤
│ 2026-02-25 │   2   │   1   │ 0.3%  │25.5 │ 18  │   1200    │   1050    │ 87.5% │...│
│            │  2.1  │   1   │ -0.1% │25   │17.5 │   1180    │   1020    │ 86.4% │   │
├─────────────────────────────────────────────────────────────────────────────────────┤
│ 2026-02-26 │   1   │   0   │ 0.1%  │26   │ 18  │   1210    │   1060    │ 87.6% │...│
│            │  2.1  │   1   │ -2.0% │25   │17.5 │   1180    │   1020    │ 86.4% │   │
└─────────────────────────────────────────────────────────────────────────────────────┘

COLOR CODING:
🟢 Verde:  Dentro de rango (-5% a +5% de guía)
🟡 Amarillo: Fuera de rango (±5% a ±15%)
🔴 Rojo:   Crítico (> ±15% o valores anomalía)
```

---

## Orden de Ejecución Recomendado

1. **G1 → G2 → G3**: Crear DTOs y endpoint backend → Swagger test
2. **H1 → H2 → H3**: Crear componentes TABs → Validar tipado Angular
3. **J1**: Agregar método HTTP en service
4. **I1 → I2 → I3**: Integrar en componente principal
5. Build + Prueba E2E completa

---

## Validaciones Críticas

- **Null Safety**: Los campos GUIA pueden ser null si no hay entrada en `ProduccionAvicolaRaw`
- **Agrupar por galpón**: Usar metadata.galpon_id o relacionar a través de LPP
- **Cálculos derivados**: Asegurarse que % se calculan en backend, no frontend
- **Semáforo**: Definir rangos por tipo de métrica (mortalidad ± 1%, peso ± 3%, uniformidad ± 5%)
- **Pesaje semanal**: Campos peso_h/peso_m solo aplican a reportes semanales

---

---

## DTOs Adicionales — Reportes Semanales

### ReporteSemanalGalponDto

Mismos campos que `ReporteDiarioGalponDto`, pero con **agregaciones semanales**:
- Mortalidad: `SUM(mortalidad_hembras + mortalidad_machos)`
- Consumo: `SUM(consumo_kg_h + consumo_kg_m)`
- Huevos: `SUM(huevo_tot, huevo_inc)` desde lunes a domingo
- Clasificación: `SUM(huevo_limpio, huevo_tratado, ...)` semanal
- Peso: `AVERAGE(peso_huevo)` semanal
- Uniformidad: `AVERAGE(uniformidad)` semanal

**DTOs para reportes consolidados (General):**

### ReporteGeneralDiarioDto
- Campos agrupados por **galpón**: se entrega una fila por galpón + fila TOTAL
- Mismos campos que ReporteDiarioGalponDto pero con `[Galpón 14] | [Galpón 15] | ... | [TOTAL]`

### ReporteGeneralSemanalDto
- Similar a ReporteGeneralDiarioDto pero con datos semanales agregados

---

## Notas de Referencia

### Tablas Base en Base de Datos

| Tabla | Uso | Campos Clave |
|-------|-----|---------|
| `produccion_diaria` | Datos diarios por galpón | fecha, semana, mortalidad_h/m, huevo_tot, huevo_inc, huevo_limpio, huevo_tratado, ... (24+ campos) |
| `ProduccionAvicolaRaw` | Tabla STANDARD (guía genética) | raza_id, año, semana_desde_encaset, peso_huevo, htaa, uniformidad, produccion |
| `lote_postura_produccion` | Sublote de producción | id, lote_postura_base_id, nombre, fecha_inicio |
| `galpones` | Información de galpones | id, nombre, capacidad, area_m2 |

### Campos de Huevos en `produccion_diaria`

**Totales:**
- `huevo_tot`: Huevos producidos (total diario)
- `huevo_inc`: Huevos incubables (aptos)
- `huevos_acumulados`: Suma acumulada desde inicio lote

**Clasificación Limpio/Sucio:**
- `huevo_limpio`: Sin defectos
- `huevo_tratado`: Limpiados/procesados
- `huevo_sucio`: Contaminados
- `huevo_deforme`: Forma anormal

**Defectos Específicos:**
- `huevo_blanco`: Sin color esperado
- `huevo_doble_yema`: Dos yemas
- `huevo_piso`: Roto en piso
- `huevo_pequeno`: Tamaño bajo
- `huevo_roto`: Cascara quebrada
- `huevo_desecho`: Rechazado
- `huevo_otro`: Otras categorías

**Incubación Externa:**
- `huevos_incubadora`: Enviados a incubadora propia
- `huevos_otras_incubadoras`: Enviados a terceros

### Metadata JSON

- Galpón ID se almacena en `produccion_diaria.metadata` como `{"galpon_id": 14, "lote_base": "K345", ...}`
- Totalidad de aves se obtiene de `lote_postura_produccion.numero_aves_inicio`

### Excel de Referencia Validado

**Archivo:** `INFORME PRODUCCION K370AB (1).xlsx`

**Estructura completa:**
- 8 hojas individuales: GALPON 14-21 (22 campos c/u)
- 4 hojas consolidadas: DIARIO A/B, SEMANAL A/B (27 campos diarios, 14 semanales)
- 2 hojas generales: DIARIO GENERAL, SEMANAL GENERAL
- 1 hoja referencia: STANDARD (tabla guía genética)
- **Total:** 15 hojas, 27-138 columnas según tipo

### Validación Confirmada

✅ Todos los campos Excel se han incluido en los DTOs actualizados
✅ Lote Base (K345) trae automáticamente todas las lotes producción (K345A, K345B)
✅ Cascada de filtros: Granja → Núcleo → Galpón → Lote Base → Lote Producción funciona en backend y frontend
✅ Campos de clasificación de huevos (11 campos) están mapeados
✅ Cálculos derivados se realizan en backend, no en frontend
