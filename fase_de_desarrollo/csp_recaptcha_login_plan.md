# Fix — el reCAPTCHA de Google desapareció del login en producción

**Fecha:** 2026-07-27 · **Tipo:** hotfix de configuración (nginx, sin tocar código Angular ni backend)

## Síntoma

En producción el login se ve "como si fuera dev": no aparece el widget de verificación de
Google (reCAPTCHA v2) debajo de "¿Olvidaste tu contraseña?". El resto de la pantalla está bien.

## Diagnóstico (verificado contra producción, no inferido)

1. El bundle desplegado **sí es de producción**. `main-HQO2PLBA.js` servido por el ALB contiene
   la `siteKey` `6LdjOggs…` y la URL `https://www.google.com/recaptcha/api.js` ⇒ el
   `fileReplacements` a `environment.prod.ts` corrió bien y `environment.production === true`.
   (El tagline "· Italcol" de la pantalla también es el de `environment.prod.ts`.)
   Descartada la hipótesis de "build de dev en prod".

2. La causa es la **CSP que empezó a aplicarse de verdad hoy**, con el commit `76a2903`
   (PWA Fase 0.C). Antes de ese commit cada `location` de `nginx.conf` declaraba sus propios
   `add_header`, lo que en nginx **descarta** los del bloque `server` padre: la CSP nunca
   llegaba al navegador y el script de Google cargaba sin restricción. Al centralizar los
   headers en `nginx-security-headers.conf` e incluirlo en cada `location`, la CSP pasó a
   viajar en todas las respuestas — y su `script-src` no contempla a Google:

   ```
   script-src 'self' 'unsafe-inline' 'unsafe-eval';   ← bloquea recaptcha/api.js
   (sin frame-src ⇒ hereda default-src 'self')        ← bloquea el <iframe> del widget
   ```

   Verificado en vivo: `curl -k -sD- https://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/`
   devuelve esa CSP, y `/version.json` = `2026-07-27T19:50:20.123Z` (el build del último deploy).

   El `@if (recaptchaEnabled && recaptchaSiteKey)` del template **sí** se cumple; el contenedor
   se renderiza vacío porque el script bloqueado nunca puede pintar el widget. De ahí que se vea
   igual que en desarrollo, donde el bloque directamente no existe.

## Cambio

Un solo archivo: `frontend/nginx-security-headers.conf`.

- `script-src`: agregar `https://www.google.com/recaptcha/` y `https://www.gstatic.com/recaptcha/`.
- `frame-src`: declararlo explícito con `'self' https://www.google.com/recaptcha/
  https://recaptcha.google.com/` (hoy hereda `default-src 'self'`, que bloquea el iframe).

Son los orígenes exactos que documenta Google para reCAPTCHA con CSP. No se afloja ninguna otra
directiva: `default-src`, `connect-src` (ya tiene `https:`, que cubre el XHR del widget),
`worker-src`, `manifest-src` y `frame-ancestors 'none'` quedan igual.

## Fuera de alcance

- No se toca `login.component.*` ni `environment.prod.ts`: el gating por `environment.production`
  es correcto y la key es válida.
- No se toca la CSP del backend (`SecurityHeadersMiddleware`): el API responde JSON, no renderiza
  el widget, y la validación del token contra Google es servidor-a-servidor (no pasa por CSP).

## Casos de prueba

1. `nginx -t` sobre la config final (contenedor efímero) ⇒ sintaxis OK.
2. Contenedor local del front: `curl -sD- localhost/` trae la CSP con los dos orígenes de Google.
3. Post-deploy en prod: la CSP en vivo incluye `recaptcha`; el login muestra el widget y se puede
   iniciar sesión (el token viaja en el payload, el backend lo valida).
4. No regresión de la Fase 0.C: la CSP y el HSTS siguen presentes en `/`, en el `.js` hasheado y
   en una ruta del SPA (es lo que ya chequea el paso C5 del workflow), y `worker-src 'self'` sigue
   ahí ⇒ el Service Worker no se rompe.
