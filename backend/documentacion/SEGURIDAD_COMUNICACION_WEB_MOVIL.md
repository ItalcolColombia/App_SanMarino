# Contrato de comunicación segura web/móvil

La API valida JWT con HS256, `iss`, `aud`, `exp`, firma y `jti`. La sesión debe existir en `sesiones_activas`; una sesión revocada, ausente o vencida responde `401`. Si la base no permite verificarla, responde `503` con `errorCode=session-validation-unavailable` y `Retry-After: 5`; el cliente conserva la sesión y la cola offline.

El navegador sólo recibe credenciales para el origen y prefijo de API configurados. El login se envía sin el bearer ni el tenant de una sesión anterior. Una respuesta 401 de una petición vieja no puede cerrar una sesión nueva. La app móvil usa `platformKey` cuando el login lo entrega y mantiene el secreto legacy sólo durante la transición.

En producción la aplicación móvil debe construirse con `API_BASE_URL=https://.../api`. Una URL HTTP en release se rechaza localmente. Dio no sigue redirecciones, porque una redirección a otro host no debe transportar bearer, firma ni datos de empresa.

El ALB debe figurar en `ReverseProxy__KnownProxies` o `ReverseProxy__KnownNetworks`; no se debe aceptar `X-Forwarded-For`/`X-Forwarded-Proto` desde clientes directos. `AllowedOrigins` contiene orígenes HTTP(S) explícitos, sin `*`, rutas, query ni fragmentos. La app móvil no depende de CORS.

Queda pendiente para una fase coordinada: refresh tokens rotatorios por dispositivo (hash y detección de reutilización), almacenamiento nativo seguro del JWT móvil, endpoint de logout que revoque la sesión actual, consumo atómico de tokens de recuperación y transacciones de cambio de contraseña. No se deben desplegar secretos del servidor dentro de una aplicación móvil como mecanismo de autenticación.
