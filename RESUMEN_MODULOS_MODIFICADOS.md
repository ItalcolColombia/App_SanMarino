# Resumen de Módulos Modificados

## 📋 Módulos y Archivos Modificados

### 1. 🔧 **MÓDULO: REPORTE TÉCNICO (Levante y Producción)**
**Objetivo:** Mejorar el reporte técnico con campos separados de mortalidad, descarte, traslados y error de sexaje.

#### Backend:
- `src/ZooSanMarino.Application/DTOs/ReporteTecnicoDto.cs`
  - ✅ Agregado `TrasladosNumero` al DTO diario
  - ✅ Separados campos en DTO semanal: `DescarteTotalSemana`, `TrasladosTotalSemana`, `ErrorSexajeTotalSemana`

- `src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs`
  - ✅ Cálculo de traslados separado de descarte
  - ✅ Actualizado `ConsolidarSemanales` para usar campos separados
  - ✅ Actualizado `ConsolidarSemanasCompletas` para consolidar nuevos campos
  - ✅ Actualizado `ConsolidarDatosDiarios` para incluir `TrasladosNumero`
  - ✅ Corrección de cálculo de edad (días y semanas)
  - ✅ Filtrado correcto de semanas 1-25 para levante

#### Frontend:
- `../frontend/src/app/features/reportes-tecnicos/services/reporte-tecnico.service.ts`
  - ✅ Agregados campos `trasladosNumero`, `descarteTotalSemana`, `trasladosTotalSemana`, `errorSexajeTotalSemana`

- `../frontend/src/app/features/reportes-tecnicos/components/tabla-datos-semanales/tabla-datos-semanales.component.html`
  - ✅ Agregadas columnas: Descarte, Traslados, Error Sexaje

- `../frontend/src/app/features/reportes-tecnicos/pages/reporte-tecnico-main/reporte-tecnico-main.component.html`
- `../frontend/src/app/features/reportes-tecnicos/pages/reporte-tecnico-main/reporte-tecnico-main.component.ts`
- `../frontend/src/app/features/reportes-tecnicos/pages/reporte-tecnico-main/reporte-tecnico-main.component.scss`

---

### 2. 🐦 **MÓDULO: TRASLADO DE AVES**
**Objetivo:** Integrar traslados con seguimiento diario de levante y producción.

#### Backend:
- `src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs`
  - ✅ Integración con `SeguimientoLoteLevante` (semanas < 26)
  - ✅ Integración con `SeguimientoProduccion` (semanas >= 26)
  - ✅ Permite valores negativos en `SelH` y `SelM` para representar traslados

#### Frontend:
- `../frontend/src/app/features/traslados-aves/pages/registros-traslados/registros-traslados.component.ts`

---

### 3. 📊 **MÓDULO: SEGUIMIENTO LOTE LEVANTE**
**Objetivo:** Mejorar seguimiento diario de lotes en levante.

#### Backend:
- `src/ZooSanMarino.Application/DTOs/SeguimientoLoteLevanteDto.cs`
- `src/ZooSanMarino.Domain/Entities/SeguimientoLoteLevante.cs`
- `src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoLoteLevanteConfiguration.cs`
- `src/ZooSanMarino.Infrastructure/Services/SeguimientoLoteLevanteService.cs`
- `src/ZooSanMarino.API/Controllers/SeguimientoLoteLevanteController.cs`

#### Frontend:
- `../frontend/src/app/features/lote-levante/pages/modal-create-edit/modal-create-edit.component.html`
- `../frontend/src/app/features/lote-levante/pages/modal-create-edit/modal-create-edit.component.ts`
- `../frontend/src/app/features/lote-levante/pages/modal-create-edit/modal-create-edit.component.scss`
- `../frontend/src/app/features/lote-levante/pages/tabla-lista-indicadores/tabla-lista-indicadores.component.html`
- `../frontend/src/app/features/lote-levante/pages/tabla-lista-indicadores/tabla-lista-indicadores.component.ts`
- `../frontend/src/app/features/lote-levante/pages/tabla-lista-indicadores/tabla-lista-indicadores.component.scss`
- `../frontend/src/app/features/lote-levante/pages/filtro-select/filtro-select.component.ts`
- `../frontend/src/app/features/lote-levante/services/seguimiento-lote-levante.service.ts`

---

### 4. 🥚 **MÓDULO: SEGUIMIENTO PRODUCCIÓN**
**Objetivo:** Mejorar seguimiento diario de lotes en producción.

#### Backend:
- `src/ZooSanMarino.Application/DTOs/Produccion/CrearSeguimientoRequest.cs`
- `src/ZooSanMarino.Application/DTOs/ProduccionLoteDto.cs`
- `src/ZooSanMarino.Domain/Entities/SeguimientoProduccion.cs`
- `src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoProduccionConfiguration.cs`
- `src/ZooSanMarino.Infrastructure/Services/SeguimientoProduccionService.cs`
- `src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs`

#### Frontend:
- `../frontend/src/app/features/lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.html`
- `../frontend/src/app/features/lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.ts`
- `../frontend/src/app/features/lote-produccion/pages/tabs-principal/tabs-principal.component.html`
- `../frontend/src/app/features/lote-produccion/pages/tabs-principal/tabs-principal.component.ts`
- `../frontend/src/app/features/lote-produccion/pages/lote-produccion-list/lote-produccion-list.component.html`
- `../frontend/src/app/features/lote-produccion/pages/lote-produccion-list/lote-produccion-list.component.ts`
- `../frontend/src/app/features/lote-produccion/services/produccion.service.ts`

---

### 5. 📦 **MÓDULO: INVENTARIO GRANJA**
**Objetivo:** Gestión de inventario de alimentos y movimientos.

#### Backend:
- `src/ZooSanMarino.Application/DTOs/FarmInventoryDtos.cs`
- `src/ZooSanMarino.Application/Interfaces/IFarmInventoryService.cs`
- `src/ZooSanMarino.Infrastructure/Services/FarmInventoryService.cs`
- `src/ZooSanMarino.API/Controllers/FarmInventoryController.cs`

#### Frontend:
- `../frontend/src/app/features/inventario/components/movimientos-form/movimientos-form.component.html`
- `../frontend/src/app/features/inventario/components/movimientos-form/movimientos-form.component.ts`
- `../frontend/src/app/features/inventario/components/movimientos-unificado-form/movimientos-unificado-form.component.html`
- `../frontend/src/app/features/inventario/components/movimientos-unificado-form/movimientos-unificado-form.component.ts`
- `../frontend/src/app/features/inventario/components/movimientos-unificado-form/movimientos-unificado-form.component.scss`
- `../frontend/src/app/features/inventario/services/inventario.service.ts`

---

### 6. 🍽️ **MÓDULO: CATÁLOGO DE ALIMENTOS**
**Objetivo:** Gestión del catálogo de alimentos.

#### Backend:
- `src/ZooSanMarino.Application/Interfaces/ICatalogItemService.cs`
- `src/ZooSanMarino.Infrastructure/Services/CatalogItemService.cs`
- `src/ZooSanMarino.API/Controllers/CatalogoAlimentosController.cs`

#### Frontend:
- `../frontend/src/app/features/catalogo-alimentos/pages/catalogo-alimentos-list/catalogo-alimentos-list.component.html`
- `../frontend/src/app/features/catalogo-alimentos/pages/catalogo-alimentos-list/catalogo-alimentos-list.component.ts`
- `../frontend/src/app/features/catalogo-alimentos/pages/catalogo-alimentos-list/catalogo-alimentos-list.component.scss`

---

### 7. 💰 **MÓDULO: REPORTE CONTABLE**
**Objetivo:** Reporte contable de lotes.

#### Backend:
- `src/ZooSanMarino.Application/DTOs/ReporteContableDto.cs`
- `src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs`

#### Frontend:
- `../frontend/src/app/features/reporte-contable/pages/reporte-contable-main/reporte-contable-main.component.html`
- `../frontend/src/app/features/reporte-contable/pages/reporte-contable-main/reporte-tecnico-main.component.ts`
- `../frontend/src/app/features/reporte-contable/services/reporte-contable.service.ts`

---

### 8. 🏠 **MÓDULO: LOTES**
**Objetivo:** Gestión general de lotes.

#### Frontend:
- `../frontend/src/app/features/lote/components/lote-list/lote-list.component.html`
- `../frontend/src/app/features/lote/components/lote-list/lote-list.component.ts`
- `../frontend/src/app/features/lote/components/modal-create-edit-lote/modal-create-edit-lote.component.html`
- `../frontend/src/app/features/lote/components/modal-create-edit-lote/modal-create-edit-lote.component.ts`

---

### 9. 🗄️ **MÓDULO: BASE DE DATOS**
**Objetivo:** Cambios en estructura de base de datos.

#### SQL:
- `sql/add_lote_padre_id_column.sql`
- `sql/add_consumo_original_columns.sql` (nuevo)
- `sql/add_metadata_column_seguimiento_levante.sql` (nuevo)
- `sql/add_metadata_column_seguimiento_produccion.sql` (nuevo)

---

### 10. 📝 **MÓDULO: DTOs NUEVOS**
**Objetivo:** Nuevos DTOs para seguimiento.

#### Backend (nuevos archivos):
- `src/ZooSanMarino.Application/DTOs/CreateSeguimientoLoteLevanteRequest.cs` (nuevo)
- `src/ZooSanMarino.Application/DTOs/CreateSeguimientoProduccionRequest.cs` (nuevo)

---

## 🎯 Prioridad de Trabajo Sugerida

### **FASE 1: Reporte Técnico (COMPLETADO ✅)**
- ✅ Separación de campos: Descarte, Traslados, Error Sexaje
- ✅ Cálculos correctos en backend
- ✅ Visualización en frontend

### **FASE 2: Traslado de Aves (COMPLETADO ✅)**
- ✅ Integración con seguimiento diario
- ✅ Descuentos automáticos en registros

### **FASE 3: Seguimiento Levante (EN PROGRESO)**
- ⚠️ Verificar integración con traslados
- ⚠️ Validar cálculos acumulados

### **FASE 4: Seguimiento Producción (EN PROGRESO)**
- ⚠️ Verificar integración con traslados
- ⚠️ Validar cálculos

### **FASE 5: Inventario y Catálogo (PENDIENTE)**
- ⏳ Revisar cambios realizados
- ⏳ Validar funcionalidad

### **FASE 6: Reporte Contable (PENDIENTE)**
- ⏳ Revisar cambios realizados
- ⏳ Validar cálculos

---

## 📌 Notas Importantes

1. **Archivos de compilación ignorados:** Se excluyeron archivos `bin/`, `obj/`, `.dll`, `.pdb`, etc.
2. **Archivos de configuración:** `angular.json`, `package.json`, `yarn.lock` también modificados pero no críticos para funcionalidad.
3. **Documentación:** Se crearon varios archivos `.md` de análisis (no rastreados por git).

---

## 🔍 Comandos Útiles

```bash
# Ver cambios en un módulo específico
git diff src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs

# Ver cambios en frontend de reportes técnicos
git diff ../frontend/src/app/features/reportes-tecnicos/

# Ver todos los cambios de un archivo específico
git diff --stat
```

