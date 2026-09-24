# Validación de claves y contrato de producción front/back — 22-sep-2026

## Enfoque arquitectónico

Auditar el contrato actual Angular → API .NET y su configuración efectiva de producción después de la mejora de seguridad. Comparar valores sensibles únicamente en memoria; reportar presencia, validez e igualdad sin revelar secretos. Distinguir cifrado de payload, almacenamiento del navegador, firma de plataforma y firma JWT exclusiva del servidor. Verificar selección de environment, precedencia de configuración y transporte/proxies.

## Archivos y responsabilidades

- Frontend: environments, servicios de cifrado/autenticación, interceptor, angular.json y Dockerfile.
- Backend: opciones JWT, cifrado, validadores de arranque, middleware, Program.cs y configuración de producción (lectura con salida redactada).
- Despliegue: workflow de producción, scripts/gates existentes y definición ECS efectiva mediante consultas de solo lectura, si hay acceso.
- Correcciones mínimas y pruebas del contrato, si se demuestra una desalineación. Este plan se ampliará con los archivos concretos antes de implementar.
- Estado y evidencia: bloque propio VALIDACION-CLAVES-PRODUCCION en tracker_estado.md; preservar el trabajo concurrente.

## Base de datos y operación

Sin cambios de BD, SQL, migraciones, rotación de claves ni despliegue. No imprimir ni persistir valores secretos descargados. Builds aislados dentro del repositorio si hay procesos ajenos; detener únicamente procesos propios.

## Reglas de negocio

1. Los valores que participan en el mismo contrato criptográfico front/back deben coincidir y usar algoritmo/formato compatibles.
2. La clave JWT pertenece al servidor; no debe copiarse al frontend para conseguir una igualdad artificial.
3. La configuración efectiva de producción prevalece sobre ejemplos o snapshots locales. Una comprobación inaccesible se reporta como pendiente, nunca como correcta.
4. Conservar contratos de login, sesiones y empresa activa. No cambiar secretos ni infraestructura productiva durante la validación.

## Casos de prueba

- Presencia/igualdad de claves de payload; compatibilidad del formato IV aleatorio prefijado; clave global/sesión de plataforma; separación de JWT/storage.
- JWT con longitud/issuer/audience requeridos por la mejora; CORS y proxies coherentes con el origen y TLS real.
- Build Angular de producción y tests de auth/interceptor; build backend y tests del módulo/solución.
- Gate de configuración con casos de coincidencia, diferencia y ausencia, si se agrega validación automatizada.
- Revisión del diff y confirmación de ausencia de secretos en la salida y archivos nuevos.

## Alcance confirmado y validación preventiva

El usuario confirmó continuar con validación local y dejar AWS pendiente, después de que las credenciales disponibles fueran rechazadas. No se certifica ni modifica ECS.

- Agregar `backend/scripts/verificar-contrato-seguridad-produccion.js` y sus tests Node con fixtures sintéticos: comparar las cuatro claves compartidas, validar requisitos JWT y los reemplazos production/docker de Angular sin imprimir valores. Leer appsettings.Production.json si existe; informar explícitamente que las variables ECS no se verifican localmente.
- Ejecutar el verificador y sus tests en `.github/workflows/deploy-production.yml`, dentro del job de tests después de instalar dependencias frontend. Preservar triggers, secuencia de despliegue y gates existentes.
- Documentar hallazgos y requisitos operativos pendientes en este plan; registrar resultados y estado solo en el bloque propio del tracker.
- La revisión adicional de proxies comprueba el efecto real de `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` del Dockerfile, que puede cambiar los defaults del framework. No inferir el comportamiento de ECS a partir de appsettings solamente.

## Hallazgos auditados

### Revalidación ante commits concurrentes

Al cerrar se detectaron commits de otras sesiones: `65f9730` agrega rotación JWT en la TaskDef y `465aff6` agrega confianza de proxy en el pipeline; HEAD de la revalidación: `65f733c`. La compilación inicial correspondía a `35dd5fe`, por lo que se reabre la verificación del backend actual. Se preservan todos esos cambios y se amplían las pruebas del gate propio para comprobar que la rotación JWT y la configuración de proxies mantienen los cuatro pares compartidos sin modificaciones.

La guarda actual `JwtClaveProduccionCalculos` rechaza la clave de desarrollo del repositorio en Production: la validación de forma/longitud local no significa que esa clave pueda usarse para arrancar producción. El pipeline actual crea la clave efectiva en `rotar-clave-jwt-taskdef.js`; la clave anterior se usa solo para validación y también debe quedar fuera del cliente. El informe de proxies de abajo describe el comportamiento sin overrides; el pipeline ahora agrega redes RFC 1918 cuando no hay configuración explícita. Su ejecución real en AWS sigue sin verificarse.

Las cuatro correspondencias locales son exactas y no requieren modificar ni rotar valores:

| Frontend production | Backend | Uso |
|---|---|---|
| `encryptionKeys.remitenteFrontend` | `Encryption:RemitenteFrontend` | Cifrar petición / descifrar en API |
| `encryptionKeys.remitenteBackend` | `Encryption:RemitenteBackend` | Cifrar respuesta / descifrar en navegador |
| `platformSecret.secretUpFrontend` | `PlatformSecret:SecretUpFrontend` | Contenido de firma legacy |
| `platformSecret.encryptionKey` | `PlatformSecret:EncryptionKey` | Cifrado de firma legacy |

Los servicios reales de Angular y .NET usan AES-256-CBC/PKCS7, PBKDF2-SHA256 con 10.000 iteraciones y el mismo salt. El IV es aleatorio por mensaje y se envía prefijado: no se configura un IV fijo compartido. Se ejercitaron WebCrypto y CryptoJS con ida/vuelta y texto Unicode, sin HTTP ni BD.

`JwtSettings:Key` se usa únicamente en el servidor para emitir y verificar HS256; `PreviousKey` solo valida. La configuración local satisface longitud UTF-8, issuer, audience y duración, pero su clave de desarrollo es rechazada por la guarda de Production actual: depende del override del deploy. Copiar cualquiera de esas claves al cliente sería un error. `PlatformSecret:DerivationKey` también pertenece solo al servidor: el backend entrega `platformKey` por sesión y el frontend ya la utiliza. La base local no define una DerivationKey utilizable, por lo que conserva el camino legacy salvo override de ECS.

`production` y `docker` reemplazan `environment.ts` por `environment.prod.ts`; el build por defecto es production. `apiUrl=/api` funciona con el interceptor endurecido. Los orígenes CORS locales tienen formato válido para la nueva validación. La clave pública reCAPTCHA y el secreto del servidor cumplen funciones distintas y no deben forzarse a ser iguales.

La coincidencia de claves embebidas en Angular prueba compatibilidad, no confidencialidad: el navegador distribuye esos valores. `platformSecret.secretUpBackend` aparece en el environment sin consumidor web; el gate lo informa sin exigir igualdad ni rotarlo. JWT y DerivationKey no aparecen configuradas en el environment. La corrección de almacenamiento/token y la retirada coordinada del camino legacy siguen fuera de esta validación.

### Configuración productiva pendiente

Por decisión explícita del usuario, AWS queda pendiente después del rechazo de las credenciales locales. No se comprobó la TaskDef efectiva, sus overrides/secretos, ni el bundle actualmente servido. El gate agregado valida archivos locales/versionados; no sustituye esa comprobación.

Antes del despliegue debe cotejarse la configuración efectiva de los cuatro pares anteriores, JWT y DerivationKey. El workflow reutiliza una TaskDef consultada por familia: también debe cotejarse con el ARN que ejecuta el servicio, sin asumir que la última revisión sea la que está corriendo.

El Dockerfile activa `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. Un probe del configurador real en .NET 10 confirmó que, sin redes explícitas, el framework vacía las listas de confianza y acepta cabeceras reenviadas desde un proxy cualquiera. Con una red explícita configurada, el configurador del repositorio la agrega después y rechaza al proxy externo. Con el flag apagado se conservan las restricciones de loopback. Es el comportamiento documentado por [Microsoft](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/8/forwarded-headers-unknown-proxies?view=aspnetcore-10.0).

El pipeline actual agrega `ReverseProxy__KnownNetworks__0..2` con redes RFC 1918 cuando no hay redes/proxies explícitos y respeta los existentes. Las pruebas locales verifican esa edición de la TaskDef y que no altere las claves compartidas. Esta tarea no cambia rangos ni el flag del Dockerfile. No hace falta crear servicios AWS adicionales para ese paso; queda pendiente verificar la configuración realmente aplicada y su adecuación a la topología productiva.

### Límite de la rotación JWT durante el rolling deployment

La rotación agrega compatibilidad de tokens viejos en el servidor nuevo (`Key` nueva + `PreviousKey` vieja), pero el servidor viejo conserva su configuración y no conoce la clave nueva. Mientras ECS mantiene ambas revisiones durante un rolling deployment, un token emitido por una instancia nueva puede recibir 401 al llegar a una vieja. Conservar `PreviousKey` no resuelve ese sentido de la compatibilidad. Tampoco conserva una clave de dos rotaciones atrás.

Por ello, igualdad de las cuatro claves AES no permite prometer ausencia de 401 durante la rotación. Antes de certificar un despliegue sin interrupciones debe verificarse la estrategia de tráfico/rotación: distribución previa de las claves de validación o cambio de tráfico que impida volver a instancias viejas después de emitir tokens nuevos. No se modificó la rotación implementada por la otra sesión ni se asumió afinidad del ALB sin evidencia de AWS.

## Reproducción y evidencia

Ejecutar desde la raíz con dependencias frontend instaladas:

```sh
node --test backend/scripts/tests/verificar-contrato-seguridad-produccion.test.js
node backend/scripts/verificar-contrato-seguridad-produccion.js
```

El gate corre también en el job `tests` del workflow de producción y bloquea incompatibilidades locales antes de desplegar. Los tests usan fixtures sintéticos y cubren ausencia/diferencias, overrides, Unicode, secretos del servidor en el cliente, selección de build y errores JSON que el binder de .NET rechaza. No se imprimen claves ni hashes.

Los resultados exactos, limitaciones de los runners y cierre de procesos están en el bloque `VALIDACION-CLAVES-PRODUCCION` de `tracker_estado.md`. Logs ignorados: `backend/artifacts/security-prod-validation-20260922/` y `frontend/artifacts/security-prod-validation-20260922/`. Probes locales sin valores persistidos: `backend/artifacts/security-prod-interop-20260922/` y `artifacts/proxy-options-probe-20260922/`.
