# Seguridad de autenticación y comunicación web/móvil — 19-sep-2026

## Objetivo y arquitectura

Fortalecer el flujo existente Angular → API .NET y Flutter → API para usuarios móviles, conservando login, empresa activa, sesiones revocables y captura offline. La API sigue siendo la autoridad de autenticación y permisos. Una firma/clave embebida en un cliente público no acredita su identidad ni reemplaza TLS/JWT.

Se corrigen riesgos comprobados antes de introducir otro mecanismo de autenticación: credenciales enviadas fuera de la API, respuestas 401 de sesiones anteriores, verificación de sesiones que acepta tokens cuando falla la BD, validación criptográfica incompleta y límites HTTP basados en cabeceras manipulables/conteos no atómicos.

## Archivos y responsabilidades

- Backend: `Application/Options/JwtOptions.cs`, `Application/Calculos/RevocacionSesionCalculos.cs`, `Infrastructure/Services/SesionActivaService.cs`, configuración JWT/CORS/pipeline en `API/Program.cs`; helpers de infraestructura para sacar configuración y respuestas del arranque.
- Middleware HTTP: `API/Middleware/RateLimitingMiddleware.cs`, reglas puras en `Application/Calculos/RateLimitingCalculos.cs`; procesamiento de proxies confiables explícitos sin aceptar cabeceras arbitrarias.
- Angular: `core/auth/auth.interceptor.ts`, helpers puros en `core/auth/funciones/` y specs. Restringir cabeceras sensibles al origen y prefijo real de la API; un 401 sólo afecta la sesión que originó la petición.
- Flutter: `core/api/api_client.dart`, modelos/sesión y tests correspondientes. Validar destino, evitar redirecciones con credenciales, usar la clave por sesión cuando el login la devuelve y leer códigos de error del cuerpo además de cabeceras. Evaluar almacenamiento seguro nativo según soporte instalado.
- Pruebas backend de reglas y pipeline HTTP/JWT con servicios sustitutos; documentación operativa en `backend/documentacion/SEGURIDAD_COMUNICACION_WEB_MOVIL.md`.

En ECS se deben configurar `ReverseProxy__KnownProxies` o `ReverseProxy__KnownNetworks` con las direcciones reales del ALB antes de depender de IP original/HSTS; la implementación conserva la confianza por defecto de loopback y no acepta `X-Forwarded-*` arbitrarios.

## BD y despliegue

Sin cambios de esquema, SQL, migraciones ni datos de producción. Builds aislados para convivir con la sesión de tickets. No realizar despliegue: debe revisarse la configuración de proxies y secretos del entorno antes de activarla en producción. No imprimir valores de secretos.

## Reglas de negocio/seguridad

1. Destino externo, ruta fuera del prefijo API o downgrade de HTTPS: jamás adjuntar credenciales de esta aplicación.
2. Respuesta 401 tardía de otra sesión: conservar la sesión actual. Un 403, 429, fallo de plataforma o 503 no equivale a sesión vencida.
3. Si no se puede verificar la revocación: rechazar la operación con 503 tipificado, conservando sesión local y cola offline; cancelación HTTP se propaga. Nunca autorizar por indisponibilidad de BD.
4. JWT firmado con HS256, clave de al menos 32 bytes UTF-8, issuer/audience/exp obligatorios; mantener duración configurada y contrato actual de login.
5. Identidad IP sólo desde conexión procesada por proxies explícitamente confiables; contadores atómicos. No confiar en `X-Forwarded-For` arbitrario ni usar rutas variables para evadir límites de autenticación.
6. CORS limitado a orígenes configurados; cabeceras de error/reintento visibles para los clientes autorizados. La app nativa no necesita habilitar CORS global.
7. Capturas y colas offline sobreviven a fallos de autenticación o conectividad.

## Casos de prueba

- Destino API válido vs host parecido, puerto/esquema diferente, ruta hermana, URL relativa, segmentos `..`, URL con usuario, URL externa y redirecciones.
- JWT expirado, mal firmado/algoritmo no permitido, issuer/audience incorrectos, sin jti/sesión, revocado, válido y verificación indisponible.
- 401 de petición antigua después de cambiar sesión; error de plataforma en cuerpo; 403/429/503 sin logout.
- Rate limit concurrente, aislamiento auth/sync, variantes de ruta, cabeceras IP falsificadas; Retry-After.
- Configuración CORS vacía/comodín y orígenes válidos; clave JWT corta/multibyte.
- `dotnet build` y `dotnet test` con salida aislada; specs Angular de auth y `yarn build`; tests/análisis Flutter si el SDK está disponible. Registrar salida real y limitaciones.

## Evolución posterior documentada

Refresh tokens rotatorios por dispositivo (hash en BD, detección de reutilización), OAuth/OIDC con Authorization Code + PKCE, almacenamiento nativo seguro y política de sesión del navegador requieren diseñar un contrato de sesión completo y migración coordinada. No simular renovación reutilizando un JWT ni llamar secreto a una clave distribuida en el cliente.

## Evidencia y cierre

El estado granular y resultados se registran únicamente en el bloque SEGURIDAD-WEB-MOVIL de `tracker_estado.md`.
