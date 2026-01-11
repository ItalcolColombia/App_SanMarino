# Solución para Actualizaciones Automáticas de la Aplicación

## Problema

Cuando se actualiza el frontend en AWS, los usuarios con sesiones activas no reciben la nueva versión automáticamente. El navegador tiene en caché el `index.html` antiguo, que referencia archivos JavaScript/CSS antiguos (con hashes antiguos). Esto causa:

- El frontend deja de comunicarse con el backend
- Los usuarios necesitan cerrar sesión y volver a iniciar sesión
- Incluso recargar la página no siempre funciona

## Solución Implementada

Se ha implementado un sistema automático de detección de versiones que:

1. **Detecta automáticamente** cuando hay una nueva versión disponible
2. **Fuerza una recarga** de la página cuando se detecta una actualización
3. **Previene el caché** de `index.html` en CloudFront
4. **Inyecta un timestamp** de build en cada versión

## Componentes

### 1. VersionCheckService (`src/app/core/services/version-check.service.ts`)

Servicio que:
- Verifica periódicamente (cada 5 minutos) si hay una nueva versión
- Compara el `index.html` actual con una versión fresca del servidor (usando cache-busting)
- Fuerza una recarga automática cuando detecta cambios

### 2. Integración en AppComponent

El `AppComponent` inicia el servicio de verificación de versiones al cargar la aplicación.

### 3. Meta Tag de Versión

Se agregó un meta tag en `index.html` con un placeholder que se reemplaza durante el build con un timestamp:
```html
<meta name="app-version" content="BUILD_TIMESTAMP_PLACEHOLDER">
```

### 4. Script de Inyección de Versión (`scripts/inject-version.js`)

Script que se ejecuta después del build para reemplazar el placeholder con un timestamp real.

### 5. Configuración de CloudFront

Se actualizó `deploy/cf-dist.json` para:
- No cachear `index.html` (TTL = 0)
- Mantener caché largo para assets estáticos con hash (que son inmutables)

## Cómo Funciona

1. **Durante el Build:**
   - Angular compila la aplicación con `outputHashing: "all"` (ya configurado)
   - El script `inject-version.js` reemplaza el placeholder con un timestamp ISO
   - Cada build tiene un timestamp único

2. **En el Navegador:**
   - Al cargar la app, se guarda la versión actual del `index.html`
   - Cada 5 minutos, se hace una petición a `/index.html?v=<timestamp>` (cache-busting)
   - Si el timestamp en el nuevo `index.html` es diferente, se detecta una nueva versión
   - Se fuerza una recarga completa de la página

3. **En CloudFront:**
   - `index.html` no se cachea (TTL = 0)
   - Los assets estáticos (JS/CSS con hash) se cachean por 1 año (son inmutables)

## Configuración

### Verificación Automática

La verificación se inicia automáticamente cuando la aplicación carga. El intervalo por defecto es de 5 minutos, pero se puede ajustar en `VersionCheckService`:

```typescript
private readonly CHECK_INTERVAL = 5 * 60 * 1000; // 5 minutos
```

### Verificación Manual

También se puede verificar manualmente:

```typescript
this.versionCheckService.checkForUpdates().subscribe(hasUpdate => {
  if (hasUpdate) {
    // Se recargará automáticamente
  }
});
```

## Despliegue

### Actualizar CloudFront

Después de actualizar el código, es necesario actualizar la configuración de CloudFront:

```bash
aws cloudfront update-distribution \
  --id <DISTRIBUTION_ID> \
  --distribution-config file://frontend/deploy/cf-dist.json
```

O usar la consola de AWS para invalidar el caché de `index.html` si es necesario.

### Invalidación de Caché (Opcional)

Si necesitas forzar una invalidación inmediata después del despliegue:

```bash
aws cloudfront create-invalidation \
  --distribution-id <DISTRIBUTION_ID> \
  --paths "/index.html"
```

## Notas Importantes

1. **No es necesario invalidar el caché manualmente** - El sistema detecta automáticamente las actualizaciones
2. **Los usuarios verán una recarga automática** cuando haya una nueva versión (después de máximo 5 minutos)
3. **El caché de CloudFront para `index.html` está deshabilitado** para asegurar que siempre se sirva la versión más reciente
4. **Los assets estáticos (JS/CSS) se cachean agresivamente** porque tienen hashes únicos y son inmutables

## Troubleshooting

### La aplicación no se actualiza automáticamente

1. Verifica que el script `inject-version.js` se ejecute durante el build
2. Verifica que CloudFront tenga TTL = 0 para `index.html`
3. Revisa la consola del navegador para ver si hay errores en el servicio de verificación

### Los usuarios aún tienen problemas

1. Verifica que la configuración de CloudFront se haya aplicado correctamente
2. Considera reducir el intervalo de verificación (aunque aumentará el tráfico)
3. Asegúrate de que el meta tag de versión esté presente en el HTML generado

## Beneficios

- ✅ Los usuarios siempre tienen la versión más reciente
- ✅ No necesitan cerrar sesión manualmente
- ✅ La detección es automática y transparente
- ✅ Minimiza problemas de compatibilidad entre frontend y backend
- ✅ No requiere intervención del usuario

