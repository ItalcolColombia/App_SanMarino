# Análisis del Módulo de Gestión de Granjas

## Estructura General

El módulo de **Gestión de Granjas** está ubicado en:
- **Componente Principal**: `frontend/src/app/features/farm/pages/farm-management/`
- **Tabs**: 3 pestañas principales
  1. **Granjas** (`app-farm-list`)
  2. **Núcleos** (`app-nucleo-list`)
  3. **Galpones** (`app-galpon-list`)

---

## 📋 TAB 1: GRANJAS (FarmListComponent)

### Ubicación
- **Componente**: `frontend/src/app/features/farm/components/farm-list/`
- **Servicio**: `frontend/src/app/features/farm/services/farm.service.ts`
- **Backend**: `backend/src/ZooSanMarino.API/Controllers/FarmController.cs`

### Lógica Actual

#### 1. **Carga de Datos** (`loadAll()`)
```typescript
forkJoin({
  farms:     this.farmSvc.getAll(),
  companies: this.companySvc.getAll(),
  regionMl:  this.masterSvc.getByKey('region_option_key'),
  paises:    this.paisSvc.getAll(),
  dptos:     this.dptoSvc.getAll(),
  ciudades:  this.ciudadSvc.getAll(),
})
```
- ✅ Carga paralela de todos los maestros
- ✅ Construye índices rápidos (`dptoById`, `ciudadById`)
- ✅ Rellena nombres de departamento/ciudad en las granjas

#### 2. **Filtros**
- **Regional**: Select con opciones del MasterList
- **Nombre**: Búsqueda exacta por nombre
- **Estado**: Activa/Inactiva/Todos
- **Búsqueda libre**: Busca en nombre, regional, compañía, departamento, ciudad

#### 3. **Formulario (Modal)**
- **Campos requeridos**: `companyId`, `name`, `status`
- **Cascada**: País → Departamento → Ciudad
- **ID sugerido**: Calcula máximo ID + 1 (solo UI, no se envía)

#### 4. **Persistencia**
- **Create**: `POST /api/Farm`
- **Update**: `PUT /api/Farm/{id}`
- **Delete**: `DELETE /api/Farm/{id}`

### Flujo Completo

#### Crear Granja
1. Usuario hace clic en "Nueva Granja"
2. Se calcula ID sugerido (máximo + 1)
3. Se abre modal con formulario vacío
4. Usuario selecciona:
   - Compañía (requerido)
   - Nombre (requerido)
   - Regional (opcional)
   - Estado (default: 'A')
   - País → Departamento → Ciudad (cascada)
5. Al guardar:
   - Valida formulario
   - Normaliza `regionalId` (si es string numérico, lo convierte; si no, usa 1)
   - Envía `CreateFarmDto` al backend
   - Recarga lista completa

#### Editar Granja
1. Usuario hace clic en "Editar" en una fila
2. Se carga la granja en el modal
3. **Problema**: Carga cascada de forma asíncrona (líneas 235-259)
   - Primero obtiene el departamento para saber `paisId`
   - Luego carga departamentos del país
   - Finalmente carga ciudades del departamento
4. Usuario modifica campos
5. Al guardar: similar a crear, pero con `UpdateFarmDto`

#### Eliminar Granja
1. Confirmación con `confirm()`
2. Llama a `delete(id)`
3. Recarga lista completa

### Problemas Identificados

#### 🔴 Críticos
1. **Cascada en edición es compleja y asíncrona** (líneas 235-259)
   - Múltiples llamadas anidadas
   - Puede fallar si el departamento no existe
   - No maneja errores adecuadamente

2. **Recarga completa después de cada operación**
   - `loadAll()` después de crear/actualizar/eliminar
   - Ineficiente para grandes volúmenes de datos

3. **Normalización de `regionalId` es frágil** (líneas 323-328)
   - Lógica compleja con regex
   - Fallback a 1 puede no ser correcto

#### 🟡 Mejoras Recomendadas
1. **Validación de unicidad de nombre por compañía**
2. **Manejo de errores más robusto** (toast notifications)
3. **Optimistic updates** en lugar de recargar todo
4. **Cache de maestros** (países, departamentos, ciudades)
5. **Paginación** si hay muchas granjas

---

## 📋 TAB 2: NÚCLEOS (NucleoListComponent)

### Ubicación
- **Componente**: `frontend/src/app/features/nucleo/components/nucleo-list/`
- **Servicio**: `frontend/src/app/features/nucleo/services/nucleo.service.ts`
- **Backend**: `backend/src/ZooSanMarino.API/Controllers/NucleoController.cs`

### Lógica Actual

#### 1. **Carga de Datos** (`ngOnInit()`)
```typescript
// Carga secuencial (no paralela)
this.farmSvc.getAll() → this.companySvc.getAll() → this.loadNucleos()
```
- ⚠️ **Problema**: Carga secuencial en lugar de paralela
- Construye mapas: `farmMap`, `companyMap`

#### 2. **Filtros**
- **Compañía**: Filtra granjas disponibles
- **Granja**: Depende de compañía seleccionada
- **Búsqueda libre**: Busca en ID, nombre, granja

#### 3. **Formulario (Modal)**
- **Campos requeridos**: `nucleoId`, `granjaId`, `nucleoNombre`
- **ID**: Se autogenera (6 dígitos aleatorios, 100000-999999)
- **Validación de unicidad**: Solo al crear (línea 250-256)

#### 4. **Persistencia**
- **Create**: `POST /api/Nucleo`
- **Update**: `PUT /api/Nucleo/{nucleoId}/{granjaId}`
- **Delete**: `DELETE /api/Nucleo/{nucleoId}/{granjaId}`

### Flujo Completo

#### Crear Núcleo
1. Usuario hace clic en "Nuevo Núcleo"
2. Se genera ID único de 6 dígitos (método `generateUniqueId6`)
3. Modal con formulario vacío
4. Usuario selecciona:
   - Granja (requerido) - filtrado por compañía si hay una seleccionada
   - Nombre (requerido)
5. Al guardar:
   - Verifica unicidad del ID (solo frontend)
   - Si existe, regenera ID
   - Envía `CreateNucleoDto`
   - **Optimistic update**: Actualiza lista local sin recargar

#### Editar Núcleo
1. Usuario hace clic en "Editar"
2. Se carga el núcleo en el modal
3. Usuario modifica campos
4. Al guardar: similar a crear, pero con `UpdateNucleoDto`

#### Eliminar Núcleo
1. Confirmación con `confirm()`
2. Llama a `delete(nucleoId, granjaId)`
3. **Optimistic update**: Remueve de lista local

### Problemas Identificados

#### 🔴 Críticos
1. **Carga secuencial de datos** (líneas 102-145)
   - Más lento que carga paralela
   - No aprovecha `forkJoin`

2. **Generación de ID puede colisionar**
   - Solo verifica en frontend
   - Backend puede rechazar si ya existe
   - No hay manejo de error si falla

3. **Validación de unicidad solo al crear**
   - No verifica si el ID ya existe en backend antes de enviar

#### 🟡 Mejoras Recomendadas
1. **Carga paralela** con `forkJoin`
2. **Validación de ID en backend** antes de crear
3. **Manejo de errores** más robusto
4. **Cache de núcleos** para evitar recargas innecesarias
5. **Filtro de granja en cascada** más claro (mostrar solo granjas de la compañía seleccionada)

---

## 📋 TAB 3: GALPONES (GalponListComponent)

### Ubicación
- **Componente**: `frontend/src/app/features/galpon/components/galpon-list/`
- **Servicio**: `frontend/src/app/features/galpon/services/galpon.service.ts`
- **Backend**: `backend/src/ZooSanMarino.API/Controllers/GalponController.cs`

### Lógica Actual

#### 1. **Carga de Datos** (`ngOnInit()`)
```typescript
forkJoin({
  companies: this.companySvc.getAll(),
  farms:     this.farmSvc.getAll(),
  nucleos:   this.nucleoSvc.getAll(),
  galpones:  this.svc.getAll()
})
```
- ✅ Carga paralela correcta
- Construye `nucleoOptions` con formato: `"Nombre (Granja X)"`

#### 2. **Filtros en Cascada**
- **Compañía** → Filtra granjas
- **Granja** → Filtra núcleos
- **Núcleo** → Filtra galpones
- **Búsqueda libre**: Busca en ID, nombre, núcleo, granja, compañía, tipo

#### 3. **Formulario (Modal)**
- **Campos requeridos**: `galponId`, `galponNombre`, `nucleoId`
- **ID**: Se autogenera (formato: `G0001`, `G0002`, etc.)
- **Auto-llenado**: Al seleccionar núcleo, se llena `granjaId` automáticamente

#### 4. **Persistencia**
- **Create**: `POST /api/Galpon`
- **Update**: `PUT /api/Galpon/{galponId}`
- **Delete**: `DELETE /api/Galpon/{galponId}`

### Flujo Completo

#### Crear Galpón
1. Usuario hace clic en "Nuevo Galpón"
2. Se genera ID sugerido: `G` + número incremental (ej: `G0001`)
3. Modal con formulario vacío
4. Usuario selecciona:
   - Núcleo (requerido) - muestra formato: "Nombre (Granja X)"
   - Nombre (requerido)
   - Ancho, Largo (opcionales)
   - Tipo de galpón (opcional, del MasterList)
5. Al seleccionar núcleo:
   - Se auto-llena `granjaId` (hidden field)
   - Se muestra granja en campo readonly
6. Al guardar:
   - Envía `CreateGalponDto`
   - Recarga lista completa (`loadGalponesAgain()`)

#### Editar Galpón
1. Usuario hace clic en "Editar"
2. Se carga el galpón en el modal
3. **ID bloqueado** (readonly)
4. Usuario modifica campos
5. Al guardar: similar a crear, pero con `UpdateGalponDto`

#### Ver Detalle
1. Usuario hace clic en "Ver detalle" (ojo)
2. Modal de solo lectura con toda la información
3. Muestra: ID, nombre, tipo, núcleo, granja, empresa, dimensiones, área, auditoría

#### Eliminar Galpón
1. Confirmación con `confirm()`
2. Llama a `delete(galponId)`
3. Recarga lista completa

### Problemas Identificados

#### 🔴 Críticos
1. **Generación de ID puede colisionar**
   - Solo busca el máximo número en IDs existentes
   - Si hay IDs no numéricos o formato diferente, falla
   - No verifica unicidad en backend

2. **Recarga completa después de cada operación**
   - `loadGalponesAgain()` recarga todos los galpones
   - Ineficiente

3. **Filtro de núcleo en modal no respeta filtros de cabecera**
   - Muestra todos los núcleos, no solo los filtrados por compañía/granja

#### 🟡 Mejoras Recomendadas
1. **Validación de ID único** antes de crear
2. **Optimistic updates** en lugar de recargar
3. **Filtro de núcleos en modal** según filtros de cabecera
4. **Cálculo de área automático** al ingresar ancho/largo
5. **Validación de dimensiones** (números positivos)

---

## 🔄 Flujos Transversales

### Relaciones entre Tabs
1. **Granja → Núcleo**: Un núcleo pertenece a una granja
2. **Núcleo → Galpón**: Un galpón pertenece a un núcleo
3. **Granja → Galpón**: Relación indirecta vía núcleo

### Problemas de Consistencia
1. **No hay validación de dependencias al eliminar**
   - ¿Se puede eliminar una granja si tiene núcleos?
   - ¿Se puede eliminar un núcleo si tiene galpones?
   - Backend debería manejar esto, pero frontend no muestra advertencias

2. **Filtros no se sincronizan entre tabs**
   - Si filtras por compañía en Granjas, no se refleja en Núcleos/Galpones

---

## 📊 Resumen de Problemas por Prioridad

### 🔴 Críticos (Alta Prioridad)
1. **Granjas**: Cascada asíncrona compleja en edición
2. **Granjas**: Recarga completa después de cada operación
3. **Núcleos**: Carga secuencial en lugar de paralela
4. **Núcleos**: Validación de ID solo en frontend
5. **Galpones**: Generación de ID puede colisionar
6. **Galpones**: Recarga completa después de cada operación

### 🟡 Importantes (Media Prioridad)
1. **Granjas**: Normalización de `regionalId` frágil
2. **Granjas**: Falta validación de unicidad de nombre
3. **Núcleos**: Manejo de errores insuficiente
4. **Galpones**: Filtro de núcleos en modal no respeta filtros
5. **Todos**: Falta manejo de errores con toast notifications
6. **Todos**: No hay validación de dependencias al eliminar

### 🟢 Mejoras (Baja Prioridad)
1. **Todos**: Cache de maestros
2. **Todos**: Paginación si hay muchos registros
3. **Todos**: Sincronización de filtros entre tabs
4. **Galpones**: Cálculo automático de área
5. **Todos**: Optimistic updates consistentes

---

## 🎯 Plan de Mejora Sugerido

### Fase 1: Correcciones Críticas
1. ✅ Convertir carga secuencial de Núcleos a paralela
2. ✅ Implementar optimistic updates en Granjas y Galpones
3. ✅ Mejorar validación de IDs únicos (frontend + backend)
4. ✅ Simplificar cascada de edición en Granjas

### Fase 2: Mejoras de UX
1. ✅ Toast notifications para errores/éxitos
2. ✅ Validación de dependencias antes de eliminar
3. ✅ Filtros sincronizados entre tabs
4. ✅ Cálculo automático de área en Galpones

### Fase 3: Optimizaciones
1. ✅ Cache de maestros
2. ✅ Paginación
3. ✅ Lazy loading de datos

---

## 📝 Notas Técnicas

### Servicios Backend
- **Farm**: `/api/Farm`
- **Nucleo**: `/api/Nucleo`
- **Galpon**: `/api/Galpon`

### DTOs Principales
- **Farm**: `FarmDto`, `CreateFarmDto`, `UpdateFarmDto`
- **Nucleo**: `NucleoDto`, `CreateNucleoDto`, `UpdateNucleoDto`
- **Galpon**: `GalponDetailDto`, `CreateGalponDto`, `UpdateGalponDto`

### Dependencias
- Todos los componentes usan `CompanyService` para obtener compañías
- Núcleos y Galpones dependen de `FarmService`
- Galpones dependen de `NucleoService`
- Granjas usan servicios de ubicación: `PaisService`, `DepartamentoService`, `CiudadService`
