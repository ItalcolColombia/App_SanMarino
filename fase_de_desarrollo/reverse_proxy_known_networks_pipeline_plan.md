# ReverseProxy:KnownNetworks por el pipeline, sin AWS (22-sep-2026)

## Problema

`441cf8f` (en `main`, no en `main-produccion`) hizo que el rate limit use `RemoteIpAddress` después de
`UseForwardedHeaders()`, en vez de leer `X-Forwarded-For` crudo. El requisito anotado era poner
`ReverseProxy__KnownNetworks__*` en la TaskDef. El usuario **no administra AWS** (no puede editar la
TaskDef en la consola), así que tiene que salir del pipeline, como la rotación de la clave JWT.

### Topología medida (consultas públicas, solo lectura)

- `zootecnico.sanmarino.com.co` es **alias directo** del ALB `sanmarino-alb-878335997` (nslookup).
  **No hay CloudFront delante**: los `backend/deploy/cloudfront-*.json` son de ene-2026 y apuntan a
  otro ALB y a un S3.
- `/api/*` lo responde **Kestrel** directo (`Server: Kestrel`); `/` lo responde nginx.
- Cadena: cliente → ALB (agrega la IP del cliente al final de `X-Forwarded-For`) → back.

### Lo que el análisis anterior no veía: `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`

El `Dockerfile` del back fija esa variable. Con ella ASP.NET Core **vacía** `KnownProxies` y
`KnownIPNetworks` (confía en cualquiera) y agrega un **segundo** `UseForwardedHeaders` al principio
del pipeline. Sin `ReverseProxy__*` en la TaskDef, las listas quedan vacías y hay dos pasadas que
confían en todo: cada una consume una entrada de `X-Forwarded-For`, así que el cliente elige su IP.

### Medición (binario de hoy con `441cf8f`, `Production`, BD falsa; «ALB» = esta máquina por su IP de red)

| Caso | Mismo cliente, XFF inventado distinto | Otro cliente |
|---|---|---|
| A) Lo que llegaría a prod hoy (variable del Dockerfile, sin `ReverseProxy`) | 99 → 99 → 99 ⇒ **se elude** | 99 |
| B) **Arreglo**: + `KnownNetworks` RFC 1918 | 99 → 98 → 97 ⇒ **no se elude** | 99 ⇒ contador propio |
| C) Sin la variable y sin `ReverseProxy` | 99 → 98 → 97 | 96 ⇒ **un contador para toda la empresa** |

## Enfoque

Confiar en las tres redes privadas RFC 1918 (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`):

- Sin AWS no se puede leer el CIDR del VPC, pero el ALB **siempre** llega al back desde una IP privada
  del VPC, y nadie en internet puede llegar al back con una IP de origen privada. Solo algo dentro del
  VPC podría declarar un `X-Forwarded-For`, igual que hoy.
- `ForwardLimit` queda en 1 (default): un solo salto, el del ALB. No hay CloudFront que exija 2.
- Con las listas ya no vacías, la doble pasada es inofensiva: la 1.ª toma la IP que agregó el ALB; en la
  2.ª el origen ya es la IP pública del cliente, que no es confiable, y no hace nada (caso B).
- **No se toca el `Dockerfile`**: sin su variable y sin este paso (deploy manual) el fallo pasaría de
  «eludible» a «toda la empresa con 429». Con el paso, los dos quedan cubiertos.

## Archivos

| Archivo | Cambio |
|---|---|
| `backend/scripts/confiar-proxy-alb-taskdef.js` | Nuevo: agrega `ReverseProxy__KnownNetworks__0..2` al contenedor `backend` si la TaskDef no trae ya `ReverseProxy__KnownNetworks__*` / `__KnownProxies__*` (si trae, respeta lo que hay). |
| `backend/scripts/tests/confiar-proxy-alb-taskdef.test.js` | Nuevo: `node --test`. |
| `.github/workflows/deploy-production.yml` | Paso «Confiar en el ALB (ReverseProxy) en la TaskDef» tras la rotación JWT, antes de «Actualizar imagen». |

Sin cambios de C#, BD, frontend ni Dockerfile. Nada en AWS: viaja en la revisión que el deploy ya
registra.

## Coordinación

La sesión VALIDACION-CLAVES-PRODUCCION (tracker abierto, sin commitear) anotó revisar el efecto de
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. Este plan lo mide y lo resuelve; no toca sus archivos ni su
gate del workflow.

## Casos de prueba

- Sin `ReverseProxy` → se agregan las 3 redes; con alguna `KnownNetworks__*` o `KnownProxies__*` (en
  `environment` o `secrets`) → sin cambios; `ForwardLimit` nunca se toca; otras variables y contenedores
  intactos; idempotente (dos pasadas = una); contenedor ausente → error.
- Las 3 redes son CIDR válidos con prefijo > 0 (lo que exige `HttpSecurityConfiguration`).
- Simulación del paso extraído del workflow encadenado con la rotación JWT; YAML.
- Medición A/B/C de arriba repetida con las variables exactas que genera el script.
