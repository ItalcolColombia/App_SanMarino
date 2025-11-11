# Análisis de Concurrencia: Preparación para 10,000 Usuarios Simultáneos

## 📋 Resumen Ejecutivo

Este documento analiza la arquitectura actual del sistema ZooSanMarino (Backend ASP.NET Core + Frontend Angular) para identificar mejoras necesarias para soportar **10,000 usuarios concurrentes** realizando actividades como registro y reportería.

**Fecha de Análisis:** 2025-01-09  
**Versión del Sistema:** Producción/Desarrollo

---

## 🔴 PROBLEMAS CRÍTICOS IDENTIFICADOS

### 1. **Connection Pooling NO Configurado**

**Ubicación:** `Program.cs` línea 127-129

**Problema:**
```csharp
builder.Services.AddDbContext<ZooSanMarinoContext>(opts =>
    opts.UseSnakeCaseNamingConvention()
        .UseNpgsql(conn));
```

**Impacto:** PostgreSQL usa por defecto un pool de 20 conexiones. Con 10,000 usuarios concurrentes, esto causará:
- Timeouts masivos
- Deadlocks
- Degradación severa del rendimiento
- Errores de conexión en cascada

**Solución Requerida:**
```csharp
builder.Services.AddDbContext<ZooSanMarinoContext>(opts =>
    opts.UseSnakeCaseNamingConvention()
        .UseNpgsql(conn, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null
            );
            npgsqlOptions.CommandTimeout(30);
            // ✅ AGREGAR CONNECTION POOLING
            npgsqlOptions.MaxPoolSize(200); // Mínimo para 10K usuarios
            npgsqlOptions.MinPoolSize(50);  // Conexiones siempre disponibles
        }));
```

**Además, en connection string:**
```
Pooling=true;MinPoolSize=50;MaxPoolSize=200;Connection Lifetime=30;Connection Idle Lifetime=5
```

---

### 2. **Rate Limiting Insuficiente**

**Ubicación:** `RateLimitingMiddleware.cs`

**Problema Actual:**
- 60 peticiones/minuto por IP (demasiado bajo)
- 5 intentos de login/minuto (muy restrictivo para 10K usuarios)
- Rate limiting en memoria (no escala entre instancias)

**Impacto:**
- Con 10K usuarios, muchos legítimos serán bloqueados
- El cache en memoria no funciona en arquitectura distribuida (load balancer)

**Solución Requerida:**
1. **Implementar Redis para rate limiting distribuido**
2. **Aumentar límites:**
   - 300-500 peticiones/minuto por IP (general)
   - 10-15 intentos de login/minuto
   - 1000 peticiones/minuto por usuario autenticado
3. **Rate limiting por usuario autenticado, no solo por IP**

---

### 3. **Ausencia de Caché Distribuido**

**Ubicación:** `Program.cs` línea 141

**Problema:**
```csharp
builder.Services.AddMemoryCache(); // ❌ Solo en memoria local
```

**Impacto:**
- Con múltiples instancias del servidor (load balancer), cada instancia tiene su propio cache
- Datos duplicados en memoria
- Inconsistencias entre instancias
- No escala horizontalmente

**Solución Requerida:**
```csharp
// Reemplazar AddMemoryCache() con Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ZooSanMarino:";
});
```

**Caché a implementar:**
- Usuarios y permisos (TTL: 15 minutos)
- Menús por usuario/rol (TTL: 30 minutos)
- Catálogos estáticos (TTL: 1 hora)
- Reportes frecuentes (TTL: 5-10 minutos)

---

### 4. **N+1 Query Problems en AuthService**

**Ubicación:** `AuthService.cs` líneas 170-265

**Problema:**
```csharp
// ❌ Múltiples queries separadas
var userCompanies = await _ctx.UserCompanies
    .Include(uc => uc.Company)
    .Where(uc => uc.UserId == user.Id)
    .ToListAsync();

var userRoles = await _ctx.UserRoles
    .Include(ur => ur.Role)
    .Where(ur => ur.UserId == user.Id)
    .ToListAsync();

var permissions = await _ctx.RolePermissions
    .Include(rp => rp.Permission)
    .Where(rp => roleIds.Contains(rp.RoleId))
    .Select(rp => rp.Permission.Key)
    .Distinct()
    .ToListAsync();
```

**Impacto:**
- Cada login ejecuta 3-5 queries separadas
- Con 10K usuarios, esto puede ser 50,000 queries/minuto
- Alto tiempo de respuesta en login

**Solución:**
```csharp
// ✅ Una sola query con múltiples includes y proyección
var loginData = await _ctx.Users
    .Where(u => u.Id == user.Id)
    .Select(u => new
    {
        User = u,
        Companies = u.UserCompanies.Select(uc => uc.Company).ToList(),
        Roles = u.UserRoles.Select(ur => ur.Role).ToList(),
        RoleIds = u.UserRoles.Select(ur => ur.RoleId).Distinct().ToList()
    })
    .FirstOrDefaultAsync();

var permissions = await _ctx.RolePermissions
    .Where(rp => loginData.RoleIds.Contains(rp.RoleId))
    .Select(rp => rp.Permission.Key)
    .Distinct()
    .ToListAsync();
```

---

### 5. **Falta de AsNoTracking en Queries de Lectura**

**Ubicación:** Múltiples servicios

**Problema Detectado:**
- Algunas queries usan `AsNoTracking()`, pero no todas
- `AuthService.LoginAsync()` no usa `AsNoTracking()` (línea 92-94)
- `UserService.GetUsersAsync()` usa `AsNoTracking()` ✅ (línea 194)

**Impacto:**
- EF Core tracking consume memoria extra
- Con 10K usuarios, esto puede acumularse rápidamente
- Slower queries

**Solución:**
- **Revisar TODOS los servicios** y agregar `AsNoTracking()` en queries de solo lectura
- Usar `AsNoTracking()` en:
  - Listados
  - Búsquedas
  - Reportes
  - Consultas de solo lectura

---

### 6. **Falta de Paginación en Endpoints Críticos**

**Ubicación:** Varios servicios

**Problema:**
- `GetUsersAsync()` carga todos los usuarios (línea 191-216)
- `GetKardexAsync()` puede retornar miles de registros sin paginación
- Reportes sin límites de resultados

**Impacto:**
- Con 10K usuarios, endpoints retornarán megabytes de datos
- Slow queries
- Alto consumo de memoria
- Timeouts

**Solución Requerida:**
```csharp
// Implementar paginación en TODOS los endpoints de listado
public async Task<PagedResult<UserListDto>> GetUsersAsync(
    int page = 1, 
    int pageSize = 50,
    string? search = null)
{
    var query = _ctx.Users.AsNoTracking();
    
    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(u => u.firstName.Contains(search) || u.surName.Contains(search));
    
    var total = await query.CountAsync();
    var items = await query
        .OrderBy(u => u.firstName)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(...)
        .ToListAsync();
    
    return new PagedResult<UserListDto>(items, total, page, pageSize);
}
```

---

### 7. **Ausencia de Índices en Base de Datos**

**Problema:**
- No se encontró documentación de índices en tablas críticas
- Probable falta de índices en:
  - `login.email` (usado en login)
  - `user_company.user_id` (usado en autenticación)
  - `user_role.user_id` (usado en autorización)
  - Campos de fecha en reportes
  - Foreign keys

**Impacto:**
- Queries lentas en tablas grandes
- Table scans masivos
- Deadlocks en alta concurrencia

**Solución Requerida:**
```sql
-- Índices críticos para autenticación
CREATE INDEX CONCURRENTLY idx_login_email ON login(email) WHERE is_deleted = false;
CREATE INDEX CONCURRENTLY idx_user_login_user_id ON user_login(user_id);
CREATE INDEX CONCURRENTLY idx_user_role_user_id ON user_role(user_id);
CREATE INDEX CONCURRENTLY idx_user_company_user_id ON user_company(user_id);

-- Índices para reportes
CREATE INDEX CONCURRENTLY idx_farm_inventory_movement_farm_date 
    ON farm_inventory_movement(farm_id, created_at);
CREATE INDEX CONCURRENTLY idx_seguimiento_produccion_lote_fecha 
    ON seguimiento_produccion(lote_id, fecha);

-- Índices compuestos
CREATE INDEX CONCURRENTLY idx_user_active ON "user"(is_active, is_locked) 
    WHERE is_active = true AND is_locked = false;
```

---

### 8. **Transacciones Largas en Operaciones Críticas**

**Ubicación:** `AuthService.RegisterAsync()` (línea 39-87)

**Problema:**
```csharp
// ❌ Múltiples operaciones en una transacción sin optimización
_ctx.Users.Add(user);
_ctx.Logins.Add(login);
_ctx.UserLogins.Add(new UserLogin { ... });

foreach (var companyId in dto.CompanyIds.Distinct())
    _ctx.UserCompanies.Add(new UserCompany { ... });

if (dto.RoleIds is not null && dto.RoleIds.Length > 0)
{
    foreach (var companyId in dto.CompanyIds.Distinct())
    foreach (var roleId in dto.RoleIds.Distinct())
    {
        _ctx.UserRoles.Add(new UserRole { ... });
    }
}

await _ctx.SaveChangesAsync(); // ❌ Una sola operación masiva
```

**Impacto:**
- Transacciones largas bloquean recursos
- Con 10K registros concurrentes, deadlocks masivos
- Timeouts

**Solución:**
```csharp
// ✅ Usar AddRange para operaciones en lote
await using var tx = await _ctx.Database.BeginTransactionAsync();
try
{
    _ctx.Users.Add(user);
    _ctx.Logins.Add(login);
    _ctx.UserLogins.Add(new UserLogin { ... });
    await _ctx.SaveChangesAsync(); // Guardar primero entidades principales
    
    // Batch inserts para relaciones
    var userCompanies = dto.CompanyIds.Distinct()
        .Select(cid => new UserCompany { UserId = user.Id, CompanyId = cid })
        .ToList();
    _ctx.UserCompanies.AddRange(userCompanies);
    
    var userRoles = dto.CompanyIds.Distinct()
        .SelectMany(cid => dto.RoleIds.Distinct().Select(rid => 
            new UserRole { UserId = user.Id, RoleId = rid, CompanyId = cid }))
        .ToList();
    _ctx.UserRoles.AddRange(userRoles);
    
    await _ctx.SaveChangesAsync();
    await tx.CommitAsync();
}
catch
{
    await tx.RollbackAsync();
    throw;
}
```

---

### 9. **Falta de Async/Await en Operaciones Síncronas**

**Ubicación:** Frontend `auth.interceptor.ts` línea 34

**Problema:**
```typescript
// ❌ Encriptación síncrona puede bloquear el hilo principal
return from(encryption.encrypt(secretUpFrontend)).pipe(
    switchMap(encryptedSecretUp => { ... })
);
```

**Impacto:**
- Bloquea el hilo principal en frontend
- Con 10K usuarios, puede causar lag en la UI

**Solución:**
- Ya está usando `from()` para convertir a Observable, pero verificar que la encriptación sea realmente asíncrona

---

### 10. **Falta de Circuit Breaker y Retry Policies**

**Problema:**
- No se encontró implementación de circuit breaker
- Sin políticas de reintento automático
- No hay degradación graceful

**Impacto:**
- Si la base de datos falla, todas las peticiones fallan
- Cascading failures
- Sin recuperación automática

**Solución Requerida:**
```csharp
// Instalar Polly
builder.Services.AddHttpClient<IRecaptchaService, RecaptchaService>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
```

---

### 11. **Kestrel Limits Insuficientes**

**Ubicación:** `Program.cs` líneas 87-94

**Problema Actual:**
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32 KB
    options.Limits.MaxRequestLineSize = 8 * 1024; // 8 KB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});
```

**Falta:**
- `MaxConcurrentConnections` (crítico para 10K usuarios)
- `MaxConcurrentUpgradedConnections`
- `MaxRequestBodySize` puede ser insuficiente para reportes grandes

**Solución:**
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 10000; // ✅ CRÍTICO
    options.Limits.MaxConcurrentUpgradedConnections = 100;
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50 MB para reportes
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024; // 64 KB
    options.Limits.MaxRequestLineSize = 16 * 1024; // 16 KB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(60);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    options.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 100, gracePeriod: TimeSpan.FromSeconds(10));
});
```

---

### 12. **Frontend: Falta de Request Debouncing y Caching**

**Problema:**
- No se encontró debouncing en búsquedas
- Sin caché de respuestas HTTP frecuentes
- Múltiples peticiones simultáneas para los mismos datos

**Impacto:**
- Duplicación de requests
- Alto tráfico de red
- Carga innecesaria en backend

**Solución:**
```typescript
// Implementar HTTP interceptor con caché
export const cacheInterceptor: HttpInterceptorFn = (req, next) => {
  // Cache GET requests por 5 minutos
  if (req.method === 'GET' && !req.url.includes('realtime')) {
    const cached = cache.get(req.url);
    if (cached) return of(cached);
    
    return next(req).pipe(
      tap(response => cache.set(req.url, response, 300000))
    );
  }
  return next(req);
};

// Debounce en búsquedas
searchTerm$.pipe(
  debounceTime(300),
  distinctUntilChanged(),
  switchMap(term => this.service.search(term))
).subscribe();
```

---

### 13. **Ausencia de Response Compression**

**Problema:**
- No se encontró configuración de compresión de respuestas
- JSON sin comprimir
- Alto ancho de banda

**Solución:**
```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

app.UseResponseCompression();
```

---

### 14. **Queries Sin Optimización de Proyección**

**Problema:**
- Muchas queries cargan entidades completas cuando solo se necesitan campos específicos
- `Select()` no usado consistentemente

**Ejemplo Problemático:**
```csharp
// ❌ Carga toda la entidad
var users = await _ctx.Users.ToListAsync();

// ✅ Debería ser
var users = await _ctx.Users
    .Select(u => new UserDto { Id = u.Id, Name = u.firstName })
    .ToListAsync();
```

---

### 15. **Falta de Health Checks Avanzados**

**Ubicación:** `Program.cs` línea 234

**Problema:**
```csharp
builder.Services.AddHealthChecks(); // ❌ Muy básico
```

**Solución:**
```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(conn, name: "postgresql", timeout: TimeSpan.FromSeconds(3))
    .AddRedis(redisConn, name: "redis", timeout: TimeSpan.FromSeconds(3))
    .AddCheck<MemoryHealthCheck>("memory", tags: new[] { "memory" });

app.MapHealthChecks("/hc", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

---

## 🟡 PROBLEMAS MODERADOS

### 16. **Logging Excesivo en Producción**

**Problema:**
- Logs detallados en cada request (JWT events)
- Console.WriteLine en producción
- Sin niveles de log configurados por ambiente

**Impacto:**
- I/O overhead
- Archivos de log masivos
- Degradación de rendimiento

**Solución:**
- Usar Serilog con niveles apropiados
- Deshabilitar logs detallados en producción
- Implementar log rotation

---

### 17. **Falta de Monitoring y APM**

**Problema:**
- Sin Application Performance Monitoring (APM)
- Sin métricas de rendimiento
- Sin alertas automáticas

**Solución:**
- Implementar Application Insights o similar
- Métricas de:
  - Response times
  - Error rates
  - Database query times
  - Memory usage
  - Connection pool usage

---

### 18. **Frontend: Sin Lazy Loading de Módulos Pesados**

**Problema:**
- Algunos módulos cargan inmediatamente
- Bundle size grande

**Solución:**
- Ya hay lazy loading en algunos módulos ✅
- Verificar que TODOS los módulos grandes usen lazy loading
- Code splitting agresivo

---

## 📊 MÉTRICAS ESTIMADAS

### Con la Configuración Actual:
- **Usuarios Concurrentes Soportados:** ~500-1,000
- **Requests por Segundo:** ~100-200
- **Tiempo de Respuesta Promedio:** 200-500ms
- **Probabilidad de Timeouts:** Alta (>10%)

### Con las Mejoras Propuestas:
- **Usuarios Concurrentes Soportados:** 10,000+
- **Requests por Segundo:** 2,000-5,000
- **Tiempo de Respuesta Promedio:** 50-150ms
- **Probabilidad de Timeouts:** Baja (<1%)

---

## 🎯 PLAN DE IMPLEMENTACIÓN PRIORIZADO

### Fase 1: CRÍTICO (Semana 1-2)
1. ✅ Configurar Connection Pooling (PostgreSQL)
2. ✅ Implementar Redis para caché distribuido
3. ✅ Agregar índices críticos en base de datos
4. ✅ Optimizar queries N+1 en AuthService
5. ✅ Configurar Kestrel limits (MaxConcurrentConnections)

### Fase 2: ALTA PRIORIDAD (Semana 3-4)
6. ✅ Implementar paginación en todos los endpoints
7. ✅ Agregar AsNoTracking() en queries de lectura
8. ✅ Implementar rate limiting con Redis
9. ✅ Agregar circuit breaker y retry policies
10. ✅ Optimizar transacciones en operaciones críticas

### Fase 3: MEDIA PRIORIDAD (Semana 5-6)
11. ✅ Implementar response compression
12. ✅ Agregar health checks avanzados
13. ✅ Optimizar proyecciones en queries
14. ✅ Implementar caché HTTP en frontend
15. ✅ Agregar debouncing en búsquedas

### Fase 4: MEJORAS CONTINUAS (Semana 7+)
16. ✅ Implementar APM (Application Insights)
17. ✅ Optimizar logging
18. ✅ Code splitting en frontend
19. ✅ Implementar CDN para assets estáticos
20. ✅ Load testing y ajustes finos

---

## 📝 CHECKLIST DE VALIDACIÓN

Antes de considerar el sistema listo para 10K usuarios:

### Backend
- [ ] Connection pool configurado (Min: 50, Max: 200)
- [ ] Redis implementado y funcionando
- [ ] Índices críticos creados
- [ ] Rate limiting con Redis
- [ ] Circuit breaker implementado
- [ ] Health checks completos
- [ ] Response compression habilitado
- [ ] Kestrel limits configurados
- [ ] Queries optimizadas (N+1 eliminados)
- [ ] Paginación en todos los listados
- [ ] AsNoTracking() en lecturas
- [ ] Transacciones optimizadas

### Frontend
- [ ] HTTP caching implementado
- [ ] Debouncing en búsquedas
- [ ] Lazy loading de módulos
- [ ] Error handling robusto
- [ ] Loading states apropiados

### Infraestructura
- [ ] Load balancer configurado
- [ ] Múltiples instancias del backend
- [ ] Redis cluster o replica set
- [ ] Database read replicas (opcional pero recomendado)
- [ ] CDN para assets estáticos
- [ ] Monitoring y alerting

### Testing
- [ ] Load testing con 10K usuarios concurrentes
- [ ] Stress testing
- [ ] Endurance testing (24 horas)
- [ ] Performance baselines establecidos

---

## 🔧 COMANDOS Y CONFIGURACIONES ESPECÍFICAS

### PostgreSQL Connection String Optimizado
```
Host=...;Port=5432;Database=...;Username=...;Password=...;
Pooling=true;
MinPoolSize=50;
MaxPoolSize=200;
Connection Lifetime=30;
Connection Idle Lifetime=5;
Timeout=15;
Command Timeout=30;
SSL Mode=Require;
Trust Server Certificate=true;
```

### Redis Connection String
```
redis://localhost:6379?abortConnect=false&connectTimeout=5000&syncTimeout=5000
```

### Variables de Entorno Recomendadas
```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5002
REDIS_CONNECTION_STRING=redis://...
DATABASE_CONNECTION_POOL_MIN=50
DATABASE_CONNECTION_POOL_MAX=200
KESTREL_MAX_CONCURRENT_CONNECTIONS=10000
```

---

## 📚 REFERENCIAS Y RECURSOS

1. **Microsoft Docs - High Performance ASP.NET Core:**
   https://docs.microsoft.com/en-us/aspnet/core/performance/

2. **PostgreSQL Connection Pooling:**
   https://www.npgsql.org/doc/connection-string-parameters.html

3. **Redis Best Practices:**
   https://redis.io/docs/manual/patterns/

4. **EF Core Performance:**
   https://docs.microsoft.com/en-us/ef/core/performance/

5. **Angular Performance:**
   https://angular.io/guide/performance

---

## ⚠️ ADVERTENCIAS IMPORTANTES

1. **NO implementar todas las mejoras a la vez.** Hacerlo incrementalmente y probar en cada paso.

2. **Load testing es CRÍTICO.** No asumir que las mejoras funcionarán sin pruebas reales.

3. **Monitorear métricas en producción** después de cada cambio.

4. **Backup de base de datos** antes de crear índices en producción (usar CONCURRENTLY).

5. **Configurar alertas** para detectar problemas antes de que afecten usuarios.

---

**Fin del Análisis**

Este documento debe ser revisado y actualizado después de cada implementación de mejoras.

