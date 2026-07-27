# Frontend — ItalGranja

Angular 22 standalone + TypeScript 6, build con `@angular/build` (esbuild/vite).

## Desarrollo

```bash
yarn start        # dev server en http://localhost:4200
yarn start:hmr    # con HMR
yarn build        # build de producción -> dist/browser
yarn test         # unit tests (Karma + Jasmine)
```

> ⚠️ Node del PATH puede quedar corto: Angular 22 exige Node ≥ 22.22.3.
> Si `yarn build` se queja de la versión, usar el Node portable
> (`~/node-portable/node-v22.23.1-win-x64`).

## Despliegue — hay UN solo origen

El frontend de producción se sirve desde **ECS + nginx detrás del ALB**, en la cuenta AWS
`196080479890`. Verificado contra prod: la respuesta trae `Server: nginx` y ningún header
de CloudFront.

| Camino | Cómo se dispara |
|---|---|
| CI/CD (el normal) | push a `main-produccion` → `.github/workflows/deploy-production.yml` |
| Manual | `make deploy-frontend` → `frontend/scripts/deploy-frontend-ecs.sh` |

El camino S3 + CloudFront que documentaba este README **ya no existe**: apuntaba a otra
cuenta AWS. Quedó en `deploy/ARCHIVADO-s3-cloudfront/` con la explicación.

⚠️ **Verificación post-deploy obligatoria** — ECS hace rollback silencioso. Ver la sección
🚀 de `CLAUDE.md` en la raíz del repo.

## Sellado de versión — no mutar el output del build

`scripts/build-version.js` corre en **dos fases** alrededor de `ng build`:

```bash
node scripts/build-version.js prepare   # buildId -> src/app/core/build-info.ts (entra al bundle)
ng build --configuration docker
node scripts/build-version.js emit      # buildId -> dist/browser/version.json
```

**Regla dura: nada puede reescribir un archivo de `dist/browser` después de `ng build`.**
El builder del Service Worker calcula el SHA1 de cada archivo mientras genera `ngsw.json`;
si el archivo cambia después, el hash no coincide, el SW arranca en *safe mode* y se
desactiva solo, en silencio. Ese era exactamente el efecto del viejo `inject-version.js`,
que reescribía `dist/browser/index.html` post-build.

En un build local `BUILD_ID` queda en `'dev'` y `VersionCheckService` se apaga.

## Caché en el borde

`nginx.conf` define qué se cachea y qué no. Los archivos de control del SW
(`ngsw.json`, `ngsw-worker.js`, `safety-worker.js`, `manifest.webmanifest`, `version.json`,
`index.html`) van **siempre `no-cache`** y en bloques `location =` que tienen que quedar
**antes** del regex de assets. Los assets con hash van `immutable` a un año.

Los headers de seguridad viven en `nginx-security-headers.conf` y **cada** `location` los
incluye: en nginx, un `add_header` dentro de un `location` descarta todos los del bloque
`server` padre.
