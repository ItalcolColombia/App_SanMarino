# Diferencia: Reporte LEVANTE vs PRODUCCIÓN

**⚠️ IMPORTANTE:** Fase 4 es SOLO para PRODUCCIÓN. LEVANTE mantiene su sistema actual (Fases 1-3).

---

## Flujo de Selección

```
┌─────────────────────────────────────────────────────────┐
│ SELECTOR DE ETAPA (En el panel de filtros)              │
├─────────────────────────────────────────────────────────┤
│  ◉ LEVANTE                                              │
│  ○ PRODUCCIÓN                                           │
└─────────────────────────────────────────────────────────┘
     │
     ├─────────────────────────────┬─────────────────────────────┐
     │                             │                             │
  LEVANTE (Actual)           PRODUCCIÓN (Fase 4 — NUEVA)
```

---

## LEVANTE — Fases 1-3 (MANTIENE SU SISTEMA ACTUAL)

### Cuando selecciona `LEVANTE`:

```
┌─────────────────────────────────────────────────────────────┐
│ REPORTE TÉCNICO — LEVANTE                                   │
├─────────────────────────────────────────────────────────────┤
│  Tabs: [🔘 Semanal] [◇ Diario]                             │
├─────────────────────────────────────────────────────────────┤
│  TABLA ÚNICA — Real vs Guía (todo consolidado)             │
│                                                             │
│  Semana│Población│Mortalidad│Consumo│Producción│Pesos│...│
│  Real  │Real     │Real      │Real   │Real      │Real │...│
│  Guía  │Guía     │Guía      │Guía   │Guía      │Guía │...│
│  Dif%  │Dif%     │Dif%      │Dif%   │Dif%      │Dif% │...│
└─────────────────────────────────────────────────────────────┘

✓ Una tabla consolidada
✓ Datos semanales (semanas 1-25 desde encasetamiento)
✓ Comparativa directa: Real | Guía | Diferencia
✓ Sin desglose por galpón
```

**Responsable:** Fases 1-3 (ya implementado)  
**Componentes:** Componente LEVANTE actual  
**Backend:** Endpoint `POST /api/ReporteTecnico/levante/obtener`  

---

## PRODUCCIÓN — Fase 4 (NUEVA - SISTEMA DE TABS)

### Cuando selecciona `PRODUCCIÓN`:

```
┌─────────────────────────────────────────────────────────────┐
│ REPORTE TÉCNICO — PRODUCCIÓN                                │
├─────────────────────────────────────────────────────────────┤
│  Tabs de Periodicidad: [🔘 Diario] [◇ Semanal]            │
│  Tabs de Ámbito:       [🔘 Por Galpón] [◇ General]        │
├─────────────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────────────┐   │
│ │ REPORTE DIARIO — Galpón 14 (Lote K345A)              │   │
│ ├──────────────────────────────────────────────────────┤   │
│ │ Fecha│Mor.H│Mor.M│%Mort│Consumo│Huevos│%Prod│...   │   │
│ │ Real │Real │Real │Calc │Total  │Total │Real │...   │   │
│ │ Guía │Guía │Guía │     │Guía   │Guía  │Guía │...   │   │
│ │ Dif  │Dif  │Dif  │Dif% │Dif    │Dif   │Dif  │...   │   │
│ ├──────────────────────────────────────────────────────┤   │
│ │ 25/02│  2  │  1  │0.3% │ 43.5  │ 1200 │87.5%│...   │   │
│ │      │ 2.1 │ 1.0 │0.4% │ 43.0  │ 1180 │86.4%│...   │   │
│ │      │🟢   │🟢   │🟢   │ 🟡    │ 🟢   │ 🟢  │...   │   │
│ └──────────────────────────────────────────────────────┘   │
│                                                             │
│ ┌──────────────────────────────────────────────────────┐   │
│ │ REPORTE DIARIO GENERAL (Consolidado todos galpones)  │   │
│ ├──────────────────────────────────────────────────────┤   │
│ │ Galpón │ Mor.H │ Mor.M │ Consumo │ Huevos │ %Prod   │   │
│ ├──────────────────────────────────────────────────────┤   │
│ │   14   │  2    │  1    │  43.5   │  1200  │ 87.5%   │   │
│ │   15   │  3    │  2    │  44.2   │  1210  │ 88.2%   │   │
│ │   16   │  2    │  1    │  42.8   │  1180  │ 86.8%   │   │
│ │ TOTAL  │  7    │  4    │ 130.5   │  3590  │ 87.5%   │   │
│ └──────────────────────────────────────────────────────┘   │
│                                                             │
│ [TAB] Semanal/Galpón  │  [TAB] Semanal/General            │
└─────────────────────────────────────────────────────────────┘

✓ Cuatro TABS diferentes (Diario/Galpón, Semanal/Galpón, Diario/General, Semanal/General)
✓ Datos diarios Y semanales (con agregaciones)
✓ Desglose por galpón (tabla por cada galpón)
✓ Tabla consolidada (todos los galpones en una tabla)
✓ Comparativa Real | Guía | Diferencia en CADA MÉTRICA
✓ Semáforo de colores (🟢 🟡 🔴)
```

**Responsable:** Fase 4 (NUEVA)  
**Componentes:** `ReportesTabsComponent` + 4 componentes tabla  
**Backend:** Endpoint `POST /api/ReporteTecnicoProduccion/obtener-tabs`  

---

## Tabla Comparativa

| Aspecto | LEVANTE (Fases 1-3) | PRODUCCIÓN (Fase 4) |
|---------|----------------------|----------------------|
| **Selector de Etapa** | LEVANTE | PRODUCCIÓN |
| **Componente Principal** | Componente LEVANTE actual | `ReportesTabsComponent` |
| **Tipo de TABs** | Semanal / Diario | Periodicidad + Ámbito |
| **Desglose por Galpón** | NO (consolidado) | SÍ (tabla por galpón) |
| **Tabla General/Consolidada** | Una tabla única | TAB separado "General" |
| **Semanas** | 1-25 desde encasetamiento | Sin límite, desde inicio producción |
| **Estructura de Datos** | Datos agregados/individuales mixtos | Desglosado: galpón + consolidado |
| **Endpoint** | `POST /levante/obtener` | `POST /obtener-tabs` |
| **DTOs Principales** | `ReporteTecnicoLevanteCompletoDto` | `ReporteTecnicoProduccionTabsDto` |
| **Campos GUIA** | Sí (desde STANDARD) | Sí (desde STANDARD) |
| **Semáforo** | Sí (en tabla) | Sí (en todas las tablas) |

---

## Cambios en el Componente Principal

### `reporte-tecnico-main.component.ts`

```typescript
// YA EXISTE — NO CAMBIAR
reporteLevante = signal<ReporteTecnicoLevanteCompletoDto | null>(null);
tabLevanteActivo: 'semanal' | 'diario' = 'semanal';

// NUEVO — AGREGAR SOLO PARA PRODUCCIÓN
reporteProduccionTabs = signal<ReporteTecnicoProduccionTabsDto | null>(null);
tabProduccionActivo: 'galpon' | 'general' = 'galpon';

// Método LEVANTE — YA EXISTE
private _generarReporteLevante(): void { ... }

// Método PRODUCCIÓN — ACTUALIZAR PARA LLAMAR A ENDPOINT /obtener-tabs
private _generarReporteProduccion(): void {
  // Llamar a obtenerReporteProduccionTabs() en lugar de obtenerReporteProduccion()
  this.reporteService.obtenerReporteProduccionTabs(request)
    .subscribe({ ... });
}
```

### `reporte-tecnico-main.component.html`

```html
<!-- LEVANTE — MANTENER COMO ESTÁ -->
@if (filterSvc.selectedEtapa() === 'LEVANTE' && reporteLevante()) {
  <section class="ux-card">
    <!-- Componentes LEVANTE actual (sin cambios) -->
  </section>
}

<!-- PRODUCCIÓN — NUEVO BLOQUE CONDICIONAL -->
@if (filterSvc.selectedEtapa() === 'PRODUCCION' && reporteProduccionTabs()) {
  <app-reportes-tabs
    [reporte]="reporteProduccionTabs()!"
    [periodicidad]="filterSvc.selectedPeriodicidad()">
  </app-reportes-tabs>
}
```

---

## Resumen: ¿QUÉ CAMBIA Y QUÉ NO?

### ✅ NO CAMBIA

- Sistema actual de LEVANTE (Fases 1-3)
- Endpoint de LEVANTE (`/levante/obtener`)
- Componentes de LEVANTE
- HTML de LEVANTE (se mantiene)
- Filtros de LEVANTE

### ✨ SE AGREGA (Fase 4 — SOLO PRODUCCIÓN)

- Nuevo endpoint PRODUCCIÓN (`/obtener-tabs`)
- Nuevos componentes de TABs (5 componentes)
- Nuevos DTOs (5 DTOs)
- Bloque condicional en HTML para PRODUCCIÓN
- Signal `reporteProduccionTabs` en componente
- Método `obtenerReporteProduccionTabs()` en service

### ⚠️ IMPORTANTE

**NO se modifica NADA de LEVANTE.** Fase 4 es 100% aislada en bloque condicional `@if (etapa === 'PRODUCCION')`.

---

## Checklist de Implementación

```
FASE 1-3: LEVANTE (YA COMPLETO)
├─ [✅] Backend LEVANTE
├─ [✅] Frontend LEVANTE
└─ [✅] Componentes LEVANTE

FASE 4: PRODUCCIÓN (NUEVA)
├─ [ ] Backend — Crear DTOs (G1)
├─ [ ] Backend — Método ObtenerReporteProduccionTabsAsync (G2)
├─ [ ] Backend — Endpoint /obtener-tabs (G3)
├─ [ ] Frontend — Componentes TABs (H1-H3)
├─ [ ] Frontend — Integración condicional (I1-I3)
├─ [ ] Frontend — Service HTTP (J1)
├─ [ ] Test E2E con PRODUCCIÓN
└─ [ ] Verificar que LEVANTE sigue funcionando
```

---

## Línea de Ejecución Recomendada

1. **Leer esta guía** (AHORA) ✅
2. **Leer especificación completa** en `03_req_reportes_tabs.md`
3. **Implementar Backend (Fase G):** 12 horas aprox
4. **Implementar Frontend (Fases H-J):** 16 horas aprox
5. **Test y ajustes:** 4 horas aprox

**Total: ~32 horas de desarrollo**

---

## Preguntas Frecuentes

**P: ¿Se modifica el reporte de LEVANTE?**  
R: NO. LEVANTE mantiene su sistema actual. Fase 4 es completamente nueva y separada.

**P: ¿Cuándo se ve el sistema de TABs?**  
R: SOLO cuando el usuario selecciona `Fase = PRODUCCIÓN` en el selector.

**P: ¿Se pueden coexistir LEVANTE y PRODUCCIÓN?**  
R: Sí, usando `@if (etapa === 'LEVANTE')` y `@if (etapa === 'PRODUCCION')` para mostrar uno u otro.

**P: ¿Los datos de LEVANTE y PRODUCCIÓN usan el mismo endpoint?**  
R: NO. LEVANTE usa `/levante/obtener` (Fase 1-3), PRODUCCIÓN usa `/obtener-tabs` (Fase 4).

**P: ¿Necesito modificar mucho código existente?**  
R: Mínimo. Solo agregar bloque condicional en HTML y signal en TypeScript. Todo lo demás de LEVANTE queda igual.
