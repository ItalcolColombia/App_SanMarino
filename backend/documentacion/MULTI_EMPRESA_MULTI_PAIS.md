# Implementación Multi-Empresa y Multi-País

## Resumen

Se ha implementado un sistema completo de multi-empresa y multi-país que permite:
- Asignar empresas a múltiples países
- Asignar usuarios a empresas en países específicos
- Validar que cada petición pertenezca a la combinación empresa-país correcta
- Filtrar datos por empresa y país en todas las consultas

## Estructura de Base de Datos

### Tabla `company_pais`
Relación muchos a muchos entre empresas y países:
- `company_id` (FK → companies)
- `pais_id` (FK → paises)
- `created_at`, `updated_at`

### Tabla `user_companies` (modificada)
Ahora incluye `pais_id`:
- `user_id` (FK → users)
- `company_id` (FK → companies)
- `pais_id` (FK → paises) ← **NUEVO**
- `is_default` (boolean)
- Clave primaria compuesta: `(user_id, company_id, pais_id)`

## Cambios en el Backend

### 1. Entidades
- **CompanyPais**: Nueva entidad para relación empresa-país
- **UserCompany**: Actualizada para incluir `PaisId`
- **Company**: Agregada navegación a `CompanyPaises`
- **Pais**: Agregada navegación a `CompanyPaises` y `UserCompanies`

### 2. Interfaces y Servicios
- **ICurrentUser**: Agregado `PaisId`
- **ICompanyPaisValidator**: Nuevo servicio para validar relaciones empresa-país-usuario
- **HttpCurrentUser**: Actualizado para leer `PaisId` del header `X-Active-Pais`

### 3. Autenticación
- **AuthService**: Actualizado para incluir información de empresa-país en la respuesta del login
- **AuthResponseDto**: Agregado campo `CompanyPaises` con lista de combinaciones empresa-país

### 4. Validación
Los servicios ahora validan que:
1. La empresa pertenece al país especificado
2. El usuario tiene acceso a esa combinación empresa-país

Ejemplo en `FarmService.GetEffectiveCompanyIdAsync()`:
```csharp
// Valida que empresa pertenece al país activo
if (_current.PaisId.HasValue)
{
    var isValid = await _ctx.CompanyPaises
        .AnyAsync(cp => cp.CompanyId == companyId && cp.PaisId == _current.PaisId.Value);
    
    if (!isValid)
        throw new UnauthorizedAccessException("Empresa no asignada al país");
}
```

## Cambios en el Frontend

### 1. Modelos
- **CompanyPais**: Nueva interfaz para combinaciones empresa-país
- **AuthSession**: Agregados campos:
  - `companyPaises`: Lista de combinaciones disponibles
  - `activeCompanyId`: ID de empresa activa
  - `activePaisId`: ID de país activo

### 2. Servicios
- **AuthService**: Procesa información de empresa-país del login
- **AuthInterceptor**: Envía header `X-Active-Pais` en todas las peticiones
- **HttpCompanyHelperService**: Actualizado para incluir `PaisId` en headers

### 3. Storage
La sesión ahora almacena:
- Lista de combinaciones empresa-país disponibles
- Empresa y país activos (seleccionados automáticamente por defecto)

## Headers HTTP

El frontend envía automáticamente:
- `X-Active-Company`: Nombre de la empresa activa
- `X-Active-Pais`: ID del país activo

El backend lee estos headers en `HttpCurrentUser` para determinar el contexto de la petición.

## Migración de Base de Datos

Ejecutar el script: `backend/sql/migracion_multi_empresa_multi_pais.sql`

Este script:
1. Crea la tabla `company_pais`
2. Agrega columna `pais_id` a `user_companies`
3. Actualiza la clave primaria de `user_companies`
4. Asigna empresas existentes a un país por defecto
5. Valida relaciones existentes

## Flujo de Trabajo

### 1. Configuración Inicial
1. Crear países en la tabla `paises`
2. Crear empresas en la tabla `companies`
3. Asignar empresas a países en `company_pais`
4. Asignar usuarios a empresas-país en `user_companies`

### 2. Login
1. Usuario inicia sesión
2. Backend retorna lista de combinaciones empresa-país disponibles
3. Frontend selecciona automáticamente la primera (o la marcada como `isDefault`)
4. Se almacena en el storage del navegador

### 3. Peticiones API
1. Frontend envía headers `X-Active-Company` y `X-Active-Pais`
2. Backend valida que:
   - La empresa pertenece al país
   - El usuario tiene acceso a esa combinación
3. Se filtran los datos por empresa y país

## Endpoints de Gestión

Se han creado endpoints completos para gestionar las relaciones empresa-país. Ver documentación detallada en: `ENDPOINTS_COMPANY_PAIS.md`

**Endpoints principales:**
- `POST /api/CompanyPais/assign` - Asignar empresa a país
- `DELETE /api/CompanyPais/remove` - Remover empresa de país
- `GET /api/CompanyPais/pais/{paisId}/companies` - Empresas por país
- `GET /api/CompanyPais/company/{companyId}/paises` - Países por empresa
- `POST /api/CompanyPais/user/assign` - Asignar usuario a empresa-país
- `DELETE /api/CompanyPais/user/remove` - Remover usuario de empresa-país
- `GET /api/CompanyPais/user/current` - Combinaciones del usuario actual

## Próximos Pasos

### Pendientes
1. **Actualizar servicios restantes**: Agregar validación empresa-país en servicios que aún no la tienen
2. **UI de selección**: Crear componente Angular para cambiar empresa-país activos
3. **Validación en middleware**: Considerar middleware global para validar empresa-país automáticamente

### Servicios que podrían necesitar actualización
- LoteService
- MovimientoAvesService
- ProduccionService
- LiquidacionTecnicaService
- InventarioAvesService
- Y otros que filtran por CompanyId

**Nota:** `FarmService` ya tiene la validación implementada como ejemplo.

## Notas Importantes

1. **Compatibilidad**: Se mantiene compatibilidad con el sistema anterior usando el campo `empresas` (legacy)
2. **Validación**: La validación empresa-país es opcional si no se envía `PaisId` (comportamiento legacy)
3. **Migración**: Los datos existentes se asignan automáticamente al país por defecto (ID=1)

## Ejemplo de Uso

### Backend - Validar acceso
```csharp
var validator = _companyPaisValidator;
var hasAccess = await validator.ValidateUserCompanyPaisAsync(
    userId, companyId, paisId);
```

### Frontend - Cambiar empresa-país activos
```typescript
// Actualizar sesión con nueva empresa-país
const session = this.storage.get();
session.activeCompanyId = newCompanyId;
session.activePaisId = newPaisId;
session.activeCompany = newCompanyName;
this.storage.save(session);
```

