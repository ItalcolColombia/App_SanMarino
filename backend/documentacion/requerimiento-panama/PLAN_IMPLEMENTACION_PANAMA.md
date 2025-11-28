# PLAN COMPLETO DE IMPLEMENTACIÓN - PROYECTO PANAMÁ
## Módulo de Registro de Medicamentos por Galpón

**Duración Total:** 15 días (3 semanas)  
**Fecha de Inicio:** [Después de implementación Ecuador]  
**Fecha de Finalización:** [A definir]

---

## RESUMEN EJECUTIVO

Este plan detalla la implementación del módulo de registro de medicamentos por galpón para Panamá, aprovechando el sistema parametrizable ya implementado para Ecuador. El módulo permitirá:

- Registro completo de medicaciones por galpón con trazabilidad
- Consultas individuales por galpón
- Resumen por granja y lote
- Cálculo automático de edad de medicación
- CRUD completo (crear, editar, borrar, visualizar)

---

## JUSTIFICACIÓN

Es necesario mantener un registro por galpón de las medicaciones administradas en la granja debido a eventualidades sanitarias u otras. Esta información debe estar vinculada al lote de producción de pollos e incluir toda la trazabilidad correspondiente.

---

## OBJETIVOS

1. **Módulo de Medicamentos:** Sistema completo de registro por galpón
2. **Trazabilidad:** Vinculación con lote de producción
3. **Consultas:** Individual por galpón y resumen por granja/lote
4. **Cálculos Automáticos:** Edad de medicación basada en fecha de encasetamiento
5. **Integración:** Aprovechar sistema parametrizable existente

---

## CAMPOS REQUERIDOS

| Campo | Tipo | Opciones | Descripción |
|-------|------|----------|-------------|
| **Granja** | Select | Lista desplegable | Nombres de las granjas activas |
| **Galpón** | Select | Lista desplegable | Listado de galpones activos |
| **Fecha de medicación** | Date | Calendario | Permite seleccionar la fecha |
| **Edad de medicación** | Calculado | Automático | Fecha medicación - Fecha encasetamiento |
| **Tipo de Medicación** | Select | Lista desplegable | Ver opciones abajo |
| **Vía de medicación** | Select | Lista desplegable | Ver opciones abajo |
| **Medicamento suministrado** | Text | Campo texto | Nombre del producto comercial |
| **Dosis** | Select | Lista desplegable | Ver opciones abajo |
| **Descripción dosis** | Number | Campo número | Ej: 20 (mg/kg) / 2 (ml/L) |
| **Tiempo de medicación/días** | Number | Campo número | Número de días de la medicación |
| **Respuesta a la medicación** | Select | Lista desplegable | Ver opciones abajo |

### Opciones de Listas Desplegables

**Tipo de Medicación:**
1. Tratamiento Antibiótico
2. Suplemento vitamínico
3. Tratamiento Anticoccidial
4. Tratamiento vías respiratorios
5. Tratamiento gastrointestinal
6. Otro

**Vía de medicación:**
1. Oral
2. Aspersión
3. Otro

**Dosis:**
1. mg/kg
2. ml/L
3. ml/ave
4. g/ave

**Respuesta a la medicación:**
1. Efectiva
2. Poco efectiva
3. No fue efectiva

---

## CRONOGRAMA COMPLETO (15 DÍAS = 3 SEMANAS)

### SEMANA 1: ANÁLISIS + BASE DE DATOS + BACKEND BASE

#### DÍA 1: Análisis y Diseño
**Backend:** 0 días | **Frontend:** 0 días | **Base de Datos:** 0 días

Tareas:
- Revisar requerimientos completos de Panamá
- Diseñar estructura de base de datos
- Diseñar entidades y DTOs
- Diseñar servicios y controladores
- Diseñar componentes frontend
- Validar con stakeholders

Entregables:
- Diseño técnico completo
- Diagramas de entidades
- Mockups de componentes

---

#### DÍA 2: Base de Datos - Tabla Medicamentos
**Backend:** 0 días | **Frontend:** 0 días | **Base de Datos:** 1 día

Tareas:
- Crear script SQL `create_medicamentos_galpon.sql`
- Crear tabla `medicamentos_galpon`
- Crear función para cálculo de edad
- Crear índices para performance
- Crear triggers para auditoría
- Ejecutar scripts en desarrollo

Script SQL:
```sql
-- Tabla principal de medicamentos por galpón
CREATE TABLE medicamentos_galpon (
    id SERIAL PRIMARY KEY,
    granja_id INTEGER NOT NULL REFERENCES farms(farm_id),
    galpon_id VARCHAR(100) NOT NULL,
    lote_id INTEGER REFERENCES lotes(lote_id),
    fecha_medicacion DATE NOT NULL,
    edad_medicacion INTEGER, -- Calculado automáticamente
    tipo_medicacion VARCHAR(100) NOT NULL,
    via_medicacion VARCHAR(50) NOT NULL,
    medicamento_suministrado VARCHAR(255) NOT NULL,
    dosis VARCHAR(50) NOT NULL,
    descripcion_dosis DECIMAL(10,2),
    tiempo_medicacion_dias INTEGER NOT NULL,
    respuesta_medicacion VARCHAR(50),
    observaciones TEXT,
    company_id INTEGER NOT NULL REFERENCES companies(company_id),
    created_by_user_id INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP
);

-- Índices para performance
CREATE INDEX idx_medicamentos_galpon_granja ON medicamentos_galpon(granja_id);
CREATE INDEX idx_medicamentos_galpon_galpon ON medicamentos_galpon(galpon_id);
CREATE INDEX idx_medicamentos_galpon_lote ON medicamentos_galpon(lote_id);
CREATE INDEX idx_medicamentos_galpon_fecha ON medicamentos_galpon(fecha_medicacion);
CREATE INDEX idx_medicamentos_galpon_company ON medicamentos_galpon(company_id);

-- Función para calcular edad de medicación
CREATE OR REPLACE FUNCTION calcular_edad_medicacion(
    p_fecha_medicacion DATE,
    p_lote_id INTEGER
)
RETURNS INTEGER AS $$
DECLARE
    v_fecha_encaset DATE;
BEGIN
    SELECT fecha_encaset INTO v_fecha_encaset
    FROM lotes
    WHERE lote_id = p_lote_id;
    
    IF v_fecha_encaset IS NULL THEN
        RETURN NULL;
    END IF;
    
    RETURN p_fecha_medicacion - v_fecha_encaset;
END;
$$ LANGUAGE plpgsql;

-- Trigger para calcular edad automáticamente
CREATE OR REPLACE FUNCTION trigger_calcular_edad_medicacion()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.lote_id IS NOT NULL THEN
        NEW.edad_medicacion := calcular_edad_medicacion(NEW.fecha_medicacion, NEW.lote_id);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER calcular_edad_medicacion_trigger
    BEFORE INSERT OR UPDATE ON medicamentos_galpon
    FOR EACH ROW
    EXECUTE FUNCTION trigger_calcular_edad_medicacion();
```

Entregables:
- Tabla de medicamentos creada
- Función de cálculo automático
- Índices para performance

---

#### DÍA 3: Backend - Entidades y DTOs
**Backend:** 1 día | **Frontend:** 0 días | **Base de Datos:** 0 días

Tareas:
- Crear entidad `MedicamentoGalpon.cs`
- Crear DTOs completos:
  - `MedicamentoGalponDto.cs`
  - `CreateMedicamentoGalponDto.cs`
  - `UpdateMedicamentoGalponDto.cs`
  - `MedicamentoGalponResumenDto.cs`
- Configurar mapeos AutoMapper
- Actualizar `ZooSanMarinoContext.cs`

Entregables:
- Entidades creadas
- DTOs completos
- Contexto actualizado

---

#### DÍA 4: Backend - Servicio y Controlador
**Backend:** 1 día | **Frontend:** 0 días | **Base de Datos:** 0 días

Tareas:
- Crear `IMedicamentoGalponService.cs`
- Implementar `MedicamentoGalponService.cs`:
  - CRUD completo
  - Consulta por galpón
  - Resumen por granja y lote
  - Validaciones
- Crear `MedicamentoGalponController.cs`
- Tests unitarios básicos

Entregables:
- Servicio completo
- Controlador con endpoints
- Tests unitarios

---

#### DÍA 5: Backend - Integración con Sistema Parametrizable
**Backend:** 1 día | **Frontend:** 0 días | **Base de Datos:** 0 días

Tareas:
- Integrar con `IPaisModuloFuncionalidadService`
- Configurar funcionalidad para Panamá
- Validar que solo se muestre para Panamá
- Tests de integración
- Documentar endpoints

Script de configuración:
```sql
-- Configurar módulo de medicamentos para Panamá (asumiendo PaisId = 3)
INSERT INTO pais_modulo_funcionalidad (pais_id, modulo, funcionalidad, activo, requerido, orden, etiqueta, descripcion)
VALUES
  (3, 'medicamentos', 'registro_medicamentos_galpon', true, true, 1, 'Registro de Medicamentos por Galpón', 'Módulo completo de registro de medicamentos por galpón'),
  (3, 'medicamentos', 'consulta_individual_galpon', true, false, 2, 'Consulta Individual por Galpón', 'Permite consultar medicamentos de un galpón específico'),
  (3, 'medicamentos', 'resumen_granja_lote', true, false, 3, 'Resumen por Granja y Lote', 'Muestra resumen de medicamentos por granja y lote');
```

Entregables:
- Integración con sistema parametrizable
- Configuración para Panamá
- Tests de integración

---

### SEMANA 2: FRONTEND - COMPONENTES Y FORMULARIOS

#### DÍA 6: Frontend - Servicios y Modelos
**Backend:** 0 días | **Frontend:** 1 día | **Base de Datos:** 0 días

Tareas:
- Crear servicio `medicamento-galpon.service.ts`
- Crear interfaces TypeScript:
  - `MedicamentoGalpon.interface.ts`
  - `CreateMedicamentoGalpon.interface.ts`
  - `MedicamentoGalponResumen.interface.ts`
- Integrar con helper de funcionalidades
- Tests de servicios

Entregables:
- Servicio frontend completo
- Interfaces TypeScript
- Tests de servicios

---

#### DÍA 7: Frontend - Componente Formulario
**Backend:** 0 días | **Frontend:** 1 día | **Base de Datos:** 0 días

Tareas:
- Crear `medicamento-galpon-form.component.ts`
- Crear `medicamento-galpon-form.component.html`
- Implementar todos los campos:
  - Selector de granja
  - Selector de galpón (filtrado por granja)
  - Calendario de fecha
  - Campo de edad (calculado automáticamente)
  - Selectores de tipo, vía, dosis, respuesta
  - Campos de texto y número
- Validaciones
- Tests de componente

Entregables:
- Formulario completo
- Validaciones implementadas
- Tests pasando

---

#### DÍA 8: Frontend - Componente Lista y Consulta Individual
**Backend:** 0 días | **Frontend:** 1 día | **Base de Datos:** 0 días

Tareas:
- Crear `medicamento-galpon-list.component.ts`
- Crear `medicamento-galpon-list.component.html`
- Implementar consulta por galpón
- Filtros y búsqueda
- Tabla con todos los campos
- Acciones: crear, editar, eliminar
- Tests de componente

Entregables:
- Lista de medicamentos
- Consulta individual por galpón
- Tests pasando

---

#### DÍA 9: Frontend - Componente Resumen
**Backend:** 0 días | **Frontend:** 1 día | **Base de Datos:** 0 días

Tareas:
- Crear `medicamento-galpon-resumen.component.ts`
- Crear `medicamento-galpon-resumen.component.html`
- Implementar resumen por granja y lote
- Mostrar galpones con y sin medicaciones
- Visualización de resumen
- Exportar resumen (opcional)
- Tests de componente

Entregables:
- Componente de resumen
- Visualización completa
- Tests pasando

---

#### DÍA 10: Frontend - Integración y Routing
**Backend:** 0 días | **Frontend:** 1 día | **Base de Datos:** 0 días

Tareas:
- Crear módulo `medicamentos-galpon.module.ts`
- Configurar routing
- Integrar con sistema de navegación
- Agregar al menú (solo para Panamá)
- Validar que solo se muestre para Panamá
- Tests de integración

Entregables:
- Módulo integrado
- Routing configurado
- Menú actualizado

---

### SEMANA 3: INTEGRACIÓN + TESTING + DEPLOYMENT

#### DÍA 11: Integración Completa
**Backend:** 0.5 días | **Frontend:** 0.5 días | **Base de Datos:** 0 días

Tareas:
- Integración completa Backend + Frontend
- Validar flujos completos:
  - Crear medicamento
  - Editar medicamento
  - Eliminar medicamento
  - Consultar por galpón
  - Ver resumen por granja/lote
- Corregir bugs de integración
- Validar cálculo automático de edad

Entregables:
- Integración completa
- Flujos validados
- Bugs corregidos

---

#### DÍA 12: Testing Exhaustivo
**Backend:** 0 días | **Frontend:** 0 días | **Base de Datos:** 0 días

Tareas:
- Tests unitarios completos
- Tests de integración
- Tests E2E de flujos principales
- Validar que solo funciona para Panamá
- Validar que no afecta otros países
- Tests de cálculo de edad
- Tests de validaciones

Entregables:
- Tests completos
- Todos los tests pasando

---

#### DÍA 13: Optimizaciones y Ajustes
**Backend:** 0.5 días | **Frontend:** 0.5 días | **Base de Datos:** 0 días

Tareas:
- Optimizar consultas
- Mejorar performance
- Ajustes de UI/UX
- Validar cálculos
- Optimizar carga de datos

Entregables:
- Optimizaciones aplicadas
- Performance mejorado

---

#### DÍA 14: Deployment Staging
**Backend:** 0 días | **Frontend:** 0 días | **Base de Datos:** 0.5 días

Tareas:
- Ejecutar scripts de migración en staging
- Deployment backend en staging
- Deployment frontend en staging
- Validar datos migrados
- Validar funcionalidades
- Testing con usuarios

Entregables:
- Sistema en staging
- Validación completa

---

#### DÍA 15: Deployment Producción + Documentación
**Backend:** 0 días | **Frontend:** 0 días | **Base de Datos:** 0.5 días

Tareas:
- Backup de base de datos
- Ejecutar scripts de migración en producción
- Deployment backend en producción
- Deployment frontend en producción
- Validar en producción
- Documentación técnica
- Guía de usuario
- Capacitación

Entregables:
- Sistema en producción
- Documentación completa
- Usuarios capacitados

---

## RESUMEN DE TIEMPOS POR ÁREA

| Área | Días Totales | Porcentaje |
|------|--------------|------------|
| **Análisis y Diseño** | 1 día | 6.7% |
| **Base de Datos** | 2 días | 13.3% |
| **Backend** | 4 días | 26.7% |
| **Frontend** | 5 días | 33.3% |
| **Integración** | 1 día | 6.7% |
| **Testing** | 1 día | 6.7% |
| **Deployment** | 1 día | 6.7% |
| **TOTAL** | **15 días** | **100%** |

---

## FUNCIONALIDADES PRINCIPALES

### 1. CRUD Completo
- Crear registro de medicamento
- Editar registro existente
- Eliminar registro (soft delete)
- Visualizar registros

### 2. Consultas
- **Individual por galpón:** Ver todos los medicamentos de un galpón específico
- **Resumen por granja y lote:** Ver resumen de todos los galpones de una granja para un lote, incluyendo galpones sin medicaciones

### 3. Cálculos Automáticos
- **Edad de medicación:** Calculada automáticamente como: Fecha medicación - Fecha encasetamiento
- Ejemplo: Medicación el 15 de feb, lote encastado el 1 de feb → Edad: 14 días

### 4. Trazabilidad
- Vinculación con lote de producción
- Historial completo de medicaciones
- Auditoría de cambios

---

## INTEGRACIÓN CON SISTEMA PARAMETRIZABLE

### Ventajas de Aprovechar Sistema Existente

1. **Sin Código Adicional:** El módulo de configuración ya existe
2. **Aislamiento Automático:** Solo se muestra para Panamá
3. **Configuración Rápida:** Solo insertar registros en `pais_modulo_funcionalidad`
4. **Escalable:** Fácil agregar a otros países si es necesario

### Configuración para Panamá

```sql
-- Activar módulo de medicamentos solo para Panamá
INSERT INTO pais_modulo_funcionalidad (pais_id, modulo, funcionalidad, activo, requerido)
VALUES
  (3, 'medicamentos', 'registro_medicamentos_galpon', true, true);
```

---

## VALIDACIONES Y REGLAS DE NEGOCIO

### Validaciones

1. **Granja y Galpón:** Deben ser activos y pertenecer a la misma empresa
2. **Fecha de medicación:** No puede ser futura
3. **Lote:** Debe existir y tener fecha de encasetamiento para calcular edad
4. **Campos requeridos:** Todos los campos son requeridos excepto observaciones
5. **Dosis:** Descripción dosis debe ser un número positivo

### Reglas de Negocio

1. **Cálculo de Edad:** Automático basado en fecha de encasetamiento del lote
2. **Trazabilidad:** Cada registro está vinculado a un lote específico
3. **Consultas:** 
   - Por galpón: Muestra todos los registros del galpón
   - Por granja/lote: Muestra resumen de todos los galpones, incluso sin medicaciones

---

## ESTRUCTURA DE DATOS

### Tabla: medicamentos_galpon

```sql
CREATE TABLE medicamentos_galpon (
    id SERIAL PRIMARY KEY,
    granja_id INTEGER NOT NULL,
    galpon_id VARCHAR(100) NOT NULL,
    lote_id INTEGER,
    fecha_medicacion DATE NOT NULL,
    edad_medicacion INTEGER, -- Calculado
    tipo_medicacion VARCHAR(100) NOT NULL,
    via_medicacion VARCHAR(50) NOT NULL,
    medicamento_suministrado VARCHAR(255) NOT NULL,
    dosis VARCHAR(50) NOT NULL,
    descripcion_dosis DECIMAL(10,2),
    tiempo_medicacion_dias INTEGER NOT NULL,
    respuesta_medicacion VARCHAR(50),
    observaciones TEXT,
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP
);
```

---

## ENDPOINTS API

### MedicamentoGalponController

- `GET /api/medicamentos-galpon` - Listar todos (con filtros)
- `GET /api/medicamentos-galpon/{id}` - Obtener por ID
- `GET /api/medicamentos-galpon/galpon/{galponId}` - Consultar por galpón
- `GET /api/medicamentos-galpon/granja/{granjaId}/lote/{loteId}/resumen` - Resumen por granja y lote
- `POST /api/medicamentos-galpon` - Crear nuevo
- `PUT /api/medicamentos-galpon/{id}` - Actualizar
- `DELETE /api/medicamentos-galpon/{id}` - Eliminar (soft delete)

---

## COMPONENTES FRONTEND

### Estructura

```
medicamentos-galpon/
├── medicamentos-galpon.module.ts
├── medicamentos-galpon-routing.module.ts
├── components/
│   ├── medicamento-galpon-form/
│   │   ├── medicamento-galpon-form.component.ts
│   │   ├── medicamento-galpon-form.component.html
│   │   └── medicamento-galpon-form.component.scss
│   ├── medicamento-galpon-list/
│   │   ├── medicamento-galpon-list.component.ts
│   │   ├── medicamento-galpon-list.component.html
│   │   └── medicamento-galpon-list.component.scss
│   └── medicamento-galpon-resumen/
│       ├── medicamento-galpon-resumen.component.ts
│       ├── medicamento-galpon-resumen.component.html
│       └── medicamento-galpon-resumen.component.scss
└── services/
    └── medicamento-galpon.service.ts
```

---

## CHECKLIST FINAL

### Semana 1
- [ ] Análisis completo
- [ ] Base de datos creada
- [ ] Backend completo

### Semana 2
- [ ] Frontend completo
- [ ] Componentes integrados
- [ ] Routing configurado

### Semana 3
- [ ] Integración completa
- [ ] Testing exhaustivo
- [ ] Deployment en producción
- [ ] Documentación completa

---

## NOTAS IMPORTANTES

1. **Aprovecha Sistema Existente:** El módulo de configuración parametrizable ya está implementado
2. **Aislamiento:** Solo se muestra para Panamá usando el sistema parametrizable
3. **Cálculo Automático:** La edad se calcula automáticamente usando trigger en BD
4. **Trazabilidad:** Cada registro está vinculado a un lote específico
5. **Consultas:** Incluye galpones sin medicaciones en el resumen

---

## VENTAJAS DE ESTA IMPLEMENTACIÓN

1. **Rápida:** Aprovecha sistema parametrizable existente
2. **Aislada:** Solo afecta a Panamá
3. **Completa:** CRUD completo con todas las funcionalidades
4. **Escalable:** Fácil agregar a otros países si es necesario
5. **Mantenible:** Código limpio y documentado

---

**Última actualización:** [Fecha]  
**Versión:** 1.0

