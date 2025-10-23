# API ProduccionAvicolaRaw - Documentación

## 📋 Resumen
Se ha creado una estructura CRUD completa para la tabla `produccion_avicola_raw` siguiendo la arquitectura hexagonal del proyecto ZooSanMarino.

## 🏗️ Estructura Implementada

### 1. **Entidad de Dominio**
- **Archivo**: `src/ZooSanMarino.Domain/Entities/ProduccionAvicolaRaw.cs`
- **Hereda de**: `AuditableEntity` (auditoría automática)
- **Propiedades**: Todas las columnas de la tabla como propiedades nullable string

### 2. **DTOs de Aplicación**
- **Archivo**: `src/ZooSanMarino.Application/DTOs/ProduccionAvicolaRawDto.cs`
- **DTOs incluidos**:
  - `ProduccionAvicolaRawDto` - Para respuestas
  - `CreateProduccionAvicolaRawDto` - Para creación
  - `UpdateProduccionAvicolaRawDto` - Para actualización
  - `ProduccionAvicolaRawSearchRequest` - Para búsquedas con filtros

### 3. **Interfaz del Servicio**
- **Archivo**: `src/ZooSanMarino.Application/Interfaces/IProduccionAvicolaRawService.cs`
- **Métodos**:
  - `CreateAsync()` - Crear registro
  - `GetAllAsync()` - Obtener todos
  - `GetByIdAsync()` - Obtener por ID
  - `UpdateAsync()` - Actualizar
  - `DeleteAsync()` - Eliminar
  - `SearchAsync()` - Búsqueda con filtros y paginación

### 4. **Implementación del Servicio**
- **Archivo**: `src/ZooSanMarino.Infrastructure/Services/ProduccionAvicolaRawService.cs`
- **Características**:
  - Mapeo automático entre entidades y DTOs
  - Filtros de búsqueda por AñoGuia y Raza
  - Paginación implementada
  - Manejo de transacciones

### 5. **Configuración de Entity Framework**
- **Archivo**: `src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionAvicolaRawConfiguration.cs`
- **Características**:
  - Mapeo a tabla `produccion_avicola_raw`
  - Configuración de columnas con snake_case
  - Índices para optimizar búsquedas
  - Auditoría automática

### 6. **Controlador API**
- **Archivo**: `src/ZooSanMarino.API/Controllers/ProduccionAvicolaRawController.cs`
- **Endpoints**:
  - `GET /api/ProduccionAvicolaRaw` - Listar todos
  - `GET /api/ProduccionAvicolaRaw/{id}` - Obtener por ID
  - `GET /api/ProduccionAvicolaRaw/search` - Búsqueda con filtros
  - `POST /api/ProduccionAvicolaRaw` - Crear
  - `PUT /api/ProduccionAvicolaRaw/{id}` - Actualizar
  - `DELETE /api/ProduccionAvicolaRaw/{id}` - Eliminar

## 🚀 Endpoints de la API

### **GET /api/ProduccionAvicolaRaw**
Obtiene todos los registros de producción avícola.

**Respuesta**: `200 OK`
```json
[
  {
    "id": 1,
    "anioGuia": "2024",
    "raza": "Cobb 500",
    "edad": "42",
    "mortSemH": "0.5",
    "retiroAcH": "0.2",
    // ... más propiedades
    "createdAt": "2024-01-01T00:00:00Z",
    "updatedAt": null
  }
]
```

### **GET /api/ProduccionAvicolaRaw/{id}**
Obtiene un registro específico por ID.

**Parámetros**:
- `id` (int): ID del registro

**Respuesta**: `200 OK` o `404 Not Found`

### **GET /api/ProduccionAvicolaRaw/search**
Búsqueda con filtros y paginación.

**Query Parameters**:
- `anioGuia` (string, opcional): Filtrar por año de guía
- `raza` (string, opcional): Filtrar por raza
- `page` (int, default: 1): Número de página
- `pageSize` (int, default: 10): Tamaño de página

**Respuesta**: `200 OK`
```json
{
  "items": [...],
  "total": 100,
  "page": 1,
  "pageSize": 10
}
```

### **POST /api/ProduccionAvicolaRaw**
Crea un nuevo registro.

**Body**:
```json
{
  "anioGuia": "2024",
  "raza": "Cobb 500",
  "edad": "42",
  "mortSemH": "0.5",
  "retiroAcH": "0.2",
  // ... más propiedades
}
```

**Respuesta**: `201 Created`

### **PUT /api/ProduccionAvicolaRaw/{id}**
Actualiza un registro existente.

**Body**: Mismo formato que POST, pero incluyendo el `id`.

**Respuesta**: `200 OK` o `404 Not Found`

### **DELETE /api/ProduccionAvicolaRaw/{id}**
Elimina un registro.

**Respuesta**: `204 No Content` o `404 Not Found`

## 🔧 Configuración

### **Registro de Dependencias**
El servicio ya está registrado en `Program.cs`:
```csharp
builder.Services.AddScoped<IProduccionAvicolaRawService, ProduccionAvicolaRawService>();
```

### **Migración de Base de Datos**
Se ha creado la migración `AddProduccionAvicolaRaw`. Para aplicarla:
```bash
dotnet ef database update
```

## 📊 Características Técnicas

### **Arquitectura Hexagonal**
- ✅ **Dominio**: Entidad pura sin dependencias
- ✅ **Aplicación**: Interfaces y DTOs
- ✅ **Infraestructura**: Implementación concreta
- ✅ **API**: Controladores REST

### **Patrones Implementados**
- ✅ **Repository Pattern**: Entity Framework como repositorio
- ✅ **Service Layer**: Lógica de negocio encapsulada
- ✅ **DTO Pattern**: Separación entre entidades y DTOs
- ✅ **Dependency Injection**: Inyección de dependencias

### **Características Avanzadas**
- ✅ **Auditoría Automática**: CreatedAt, UpdatedAt automáticos
- ✅ **Paginación**: Búsquedas paginadas
- ✅ **Filtros**: Búsqueda por múltiples criterios
- ✅ **Validación**: ModelState validation
- ✅ **Logging**: Logging estructurado
- ✅ **Manejo de Errores**: Respuestas HTTP apropiadas

## 🎯 Uso desde el Frontend

### **Ejemplo con JavaScript/Fetch**
```javascript
// Obtener todos los registros
const response = await fetch('/api/ProduccionAvicolaRaw');
const data = await response.json();

// Buscar con filtros
const searchResponse = await fetch('/api/ProduccionAvicolaRaw/search?anioGuia=2024&raza=Cobb&page=1&pageSize=20');
const searchData = await searchResponse.json();

// Crear nuevo registro
const newRecord = await fetch('/api/ProduccionAvicolaRaw', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    anioGuia: '2024',
    raza: 'Cobb 500',
    edad: '42',
    // ... más propiedades
  })
});
```

## ✅ Estado del Proyecto

- ✅ **Compilación**: Exitosa
- ✅ **Migración**: Creada
- ✅ **Dependencias**: Registradas
- ✅ **API**: Lista para usar
- ✅ **Documentación**: Completa

La estructura CRUD está completamente implementada y lista para ser consumida desde el frontend.
