# REQUERIMIENTOS DETALLADOS - MÓDULO MEDICAMENTOS PANAMÁ
## Registro de Medicamentos por Galpón

---

## 1. JUSTIFICACIÓN

Es necesario mantener un registro por galpón de las medicaciones administradas en la granja debido a eventualidades sanitarias u otras. Esta información debe estar vinculada al lote de producción de pollos e incluir toda la trazabilidad correspondiente.

---

## 2. DESCRIPCIÓN GENERAL

El desarrollo debe ofrecer versatilidad para crear, editar, borrar y visualizar la información consignada. Es importante poder consultar esta información individualmente por galpón. Además, cuando se consulte por granja y lote, la aplicación debe permitir visualizar un resumen de los galpones que constituyen la granja para ese lote de producción, incluyendo los casos donde no se ha hecho intervención (indicado en un espacio en blanco).

---

## 3. REQUERIMIENTOS - CAMPOS DEL FORMULARIO

### 3.1 Granja
- **Tipo:** Lista desplegable
- **Opciones:** Nombres de las granjas activas
- **Requerido:** Sí
- **Validación:** Debe ser una granja activa de la empresa del usuario

### 3.2 Galpón
- **Tipo:** Lista desplegable
- **Opciones:** Listado de galpones activos
- **Requerido:** Sí
- **Validación:** 
  - Debe ser un galpón activo
  - Debe pertenecer a la granja seleccionada
  - Se filtra automáticamente según la granja seleccionada

### 3.3 Fecha de medicación
- **Tipo:** Calendario (Date picker)
- **Requerido:** Sí
- **Validación:** 
  - No puede ser una fecha futura
  - Debe ser una fecha válida

### 3.4 Edad de medicación
- **Tipo:** Campo calculado automáticamente
- **Requerido:** No (se calcula automáticamente)
- **Cálculo:** 
  - Fórmula: `Fecha de medicación - Fecha de encasetamiento del lote`
  - Ejemplo: 
    - Medicación el 15 de febrero
    - Lote encastado el 1 de febrero
    - Edad calculada: 15 - 1 = 14 días
- **Validación:** 
  - Solo se calcula si el galpón tiene un lote asociado
  - Si no hay lote o fecha de encasetamiento, queda en blanco

### 3.5 Tipo de Medicación
- **Tipo:** Lista desplegable
- **Opciones:**
  1. Tratamiento Antibiótico
  2. Suplemento vitamínico
  3. Tratamiento Anticoccidial
  4. Tratamiento vías respiratorios
  5. Tratamiento gastrointestinal
  6. Otro
- **Requerido:** Sí

### 3.6 Vía de medicación
- **Tipo:** Lista desplegable
- **Opciones:**
  1. Oral
  2. Aspersión
  3. Otro
- **Requerido:** Sí

### 3.7 Medicamento suministrado
- **Tipo:** Campo de texto
- **Requerido:** Sí
- **Validación:** 
  - Máximo 255 caracteres
  - Se escribe el nombre del producto comercial

### 3.8 Dosis
- **Tipo:** Lista desplegable
- **Opciones:**
  1. mg/kg
  2. ml/L
  3. ml/ave
  4. g/ave
- **Requerido:** Sí

### 3.9 Descripción dosis
- **Tipo:** Campo numérico (decimal)
- **Requerido:** Sí
- **Validación:** 
  - Debe ser un número positivo
  - Máximo 2 decimales
  - Ejemplo: 20 (si dosis es mg/kg) o 2 (si dosis es ml/L)
  - Se puede escribir como: "20 (mg/kg) / 2 (ml/L)"

### 3.10 Tiempo de la medicación/días
- **Tipo:** Campo numérico (entero)
- **Requerido:** Sí
- **Validación:** 
  - Debe ser un número entero positivo
  - Ejemplo: 5 (días)

### 3.11 Respuesta a la medicación
- **Tipo:** Lista desplegable
- **Opciones:**
  1. Efectiva
  2. Poco efectiva
  3. No fue efectiva
- **Requerido:** No (opcional)

### 3.12 Observaciones
- **Tipo:** Campo de texto largo (textarea)
- **Requerido:** No (opcional)
- **Validación:** 
  - Máximo 1000 caracteres

---

## 4. FUNCIONALIDADES REQUERIDAS

### 4.1 CRUD Completo

#### Crear
- Formulario con todos los campos
- Validaciones en tiempo real
- Cálculo automático de edad
- Guardar registro en base de datos
- Mensaje de confirmación

#### Editar
- Cargar datos existentes en formulario
- Permitir modificar todos los campos
- Recalcular edad si cambia fecha o lote
- Actualizar registro en base de datos
- Mensaje de confirmación

#### Eliminar
- Confirmación antes de eliminar
- Soft delete (marcar como eliminado, no borrar físicamente)
- Mantener trazabilidad
- Mensaje de confirmación

#### Visualizar
- Lista de todos los registros
- Filtros y búsqueda
- Detalle individual
- Exportar datos (opcional)

### 4.2 Consulta Individual por Galpón

- Selector de galpón
- Mostrar todos los medicamentos del galpón seleccionado
- Ordenar por fecha (más reciente primero)
- Filtros adicionales:
  - Por tipo de medicación
  - Por rango de fechas
  - Por respuesta a medicación

### 4.3 Resumen por Granja y Lote

- Selector de granja
- Selector de lote (filtrado por granja)
- Mostrar resumen de todos los galpones de la granja para ese lote
- Incluir galpones sin medicaciones (mostrar en blanco)
- Información mostrada por galpón:
  - ID/Nombre del galpón
  - Tiene medicaciones (Sí/No)
  - Total de medicaciones
  - Última fecha de medicación
  - Tipos de medicación aplicados
- Exportar resumen (opcional)

---

## 5. REGLAS DE NEGOCIO

### 5.1 Cálculo de Edad

- La edad se calcula automáticamente cuando:
  - Se selecciona un galpón que tiene un lote asociado
  - El lote tiene fecha de encasetamiento
  - Se ingresa o modifica la fecha de medicación
- Fórmula: `Edad = Fecha de medicación - Fecha de encasetamiento`
- Si no hay lote o fecha de encasetamiento, el campo queda en blanco

### 5.2 Trazabilidad

- Cada registro debe estar vinculado a:
  - Una granja específica
  - Un galpón específico
  - Un lote de producción (opcional pero recomendado)
- El sistema debe mantener historial de:
  - Usuario que creó el registro
  - Fecha de creación
  - Usuario que modificó (si aplica)
  - Fecha de modificación

### 5.3 Validaciones

- Granja y galpón deben ser activos
- Galpón debe pertenecer a la granja seleccionada
- Fecha de medicación no puede ser futura
- Si hay lote, debe existir y tener fecha de encasetamiento para calcular edad
- Todos los campos requeridos deben estar completos antes de guardar

### 5.4 Consultas

- **Por galpón:** Muestra solo los registros del galpón seleccionado
- **Resumen por granja/lote:** 
  - Muestra todos los galpones de la granja
  - Filtra por lote si está especificado
  - Incluye galpones sin medicaciones
  - Muestra información resumida

---

## 6. INTERFAZ DE USUARIO

### 6.1 Formulario de Registro

- Diseño limpio y organizado
- Campos agrupados lógicamente:
  - Información básica (Granja, Galpón, Fecha)
  - Información de medicación (Tipo, Vía, Medicamento, Dosis)
  - Información adicional (Tiempo, Respuesta, Observaciones)
- Validaciones visuales:
  - Campos requeridos marcados con asterisco
  - Mensajes de error en tiempo real
  - Indicador de cálculo automático para edad
- Responsive design

### 6.2 Lista de Medicamentos

- Tabla con columnas principales:
  - Fecha
  - Galpón
  - Tipo de medicación
  - Medicamento
  - Dosis
  - Respuesta
  - Acciones (Editar, Eliminar)
- Filtros:
  - Por granja
  - Por galpón
  - Por tipo de medicación
  - Por rango de fechas
- Búsqueda por texto (medicamento)
- Paginación

### 6.3 Resumen por Granja y Lote

- Vista de resumen tipo tabla o cards
- Columnas/Información:
  - Galpón
  - Tiene medicaciones (indicador visual)
  - Total de medicaciones
  - Última medicación
  - Tipos aplicados
- Indicador visual para galpones sin medicaciones
- Opción de ver detalle de cada galpón

---

## 7. INTEGRACIÓN CON SISTEMA EXISTENTE

### 7.1 Sistema Parametrizable

- El módulo se activa solo para Panamá usando el sistema parametrizable
- Configuración en tabla `pais_modulo_funcionalidad`
- No afecta a otros países

### 7.2 Menú de Navegación

- Agregar opción "Medicamentos" al menú principal
- Solo visible para usuarios de Panamá
- Submenú:
  - Registrar Medicamento
  - Consultar por Galpón
  - Resumen por Granja/Lote

### 7.3 Permisos

- Integrar con sistema de permisos existente
- Permisos sugeridos:
  - `medicamentos:create` - Crear registros
  - `medicamentos:read` - Ver registros
  - `medicamentos:update` - Editar registros
  - `medicamentos:delete` - Eliminar registros

---

## 8. CASOS DE USO

### Caso de Uso 1: Registrar Nueva Medicación

1. Usuario selecciona "Registrar Medicamento"
2. Selecciona Granja
3. Selecciona Galpón (filtrado por granja)
4. Ingresa Fecha de medicación
5. Sistema calcula automáticamente Edad (si hay lote)
6. Selecciona Tipo de medicación
7. Selecciona Vía de medicación
8. Ingresa nombre del Medicamento
9. Selecciona unidad de Dosis
10. Ingresa Descripción de dosis
11. Ingresa Tiempo de medicación en días
12. Selecciona Respuesta (opcional)
13. Ingresa Observaciones (opcional)
14. Guarda registro
15. Sistema muestra confirmación

### Caso de Uso 2: Consultar por Galpón

1. Usuario selecciona "Consultar por Galpón"
2. Selecciona Galpón
3. Sistema muestra lista de medicamentos del galpón
4. Usuario puede filtrar por tipo, fecha, respuesta
5. Usuario puede ver detalle de cada medicamento
6. Usuario puede editar o eliminar registros

### Caso de Uso 3: Ver Resumen por Granja y Lote

1. Usuario selecciona "Resumen por Granja/Lote"
2. Selecciona Granja
3. Selecciona Lote (opcional)
4. Sistema muestra resumen de todos los galpones
5. Muestra galpones con y sin medicaciones
6. Usuario puede hacer clic en galpón para ver detalle
7. Usuario puede exportar resumen (opcional)

---

## 9. VALIDACIONES TÉCNICAS

### Backend

- Validar que granja existe y está activa
- Validar que galpón existe, está activo y pertenece a la granja
- Validar que lote existe (si se proporciona)
- Validar formato de fecha
- Validar que fecha no sea futura
- Validar valores de listas desplegables
- Validar rangos numéricos
- Calcular edad automáticamente

### Frontend

- Validaciones en tiempo real
- Mensajes de error claros
- Deshabilitar campos según contexto
- Mostrar cálculo de edad automáticamente
- Filtrar galpones según granja seleccionada

---

## 10. DATOS DE PRUEBA

### Ejemplo de Registro

```
Granja: Granja Norte
Galpón: Galpón 3
Fecha de medicación: 2025-02-15
Edad de medicación: 14 días (calculado)
Tipo de medicación: Tratamiento Antibiótico
Vía de medicación: Oral
Medicamento suministrado: Amoxicilina 20%
Dosis: mg/kg
Descripción dosis: 20
Tiempo de medicación/días: 5
Respuesta a la medicación: Efectiva
Observaciones: Aplicado en agua de bebida
```

---

**Última actualización:** [Fecha]  
**Versión:** 1.0

