# ARCHIVADO — camino de despliegue S3 + CloudFront (NO está en uso)

**Fecha de archivo:** 2026-07-27 · **Motivo:** el repo tenía dos caminos de despliegue del
frontend vivos a la vez, con políticas de caché incompatibles. Eso es un incidente pendiente
en cuanto se agregue un Service Worker, así que se dejó **uno solo**.

## Cuál es el origen real (verificado, no supuesto)

```
$ curl -sk -D - https://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/ -o /dev/null
HTTP/1.1 200 OK
Server: nginx          <-- nginx del contenedor ECS, directo
                       <-- sin `Via:`, sin `X-Cache:`, sin `X-Amz-Cf-Id:` de CloudFront
```

El frontend de producción se sirve desde **ECS + nginx detrás del ALB**, que es exactamente
lo que despliegan `.github/workflows/deploy-production.yml` (job `deploy-frontend`) y
`make deploy-frontend` (`frontend/scripts/deploy-frontend-ecs.sh`).

## Por qué estos archivos están muertos

Describen una distribución CloudFront sobre un bucket S3 en **otra cuenta AWS**:

| | Cuenta | Recurso |
|---|---|---|
| Estos archivos | `021891592771` | bucket `sanmarino-frontend-021891592771-us-east-2`, distribución `EBH3ELXXF2N7T`, ALB `alb-sanmarino-1757251809` |
| Pipeline real | `196080479890` | ECR `sanmarino/zootecnia/granjas/frontend`, cluster `devSanmarinoZoo`, ALB `sanmarino-alb-878335997` |

Ningún workflow, Makefile ni script del repo los consume.

## Qué NO se archivó

Siguen en `frontend/deploy/` porque sí están en uso:
`ecs-taskdef.json`, `ecs-taskdef-us-east-1.json`, `ecr-policy-frontend.json`.

## Si algún día se vuelve a poner un CDN por delante

Hay que replicar lo que hoy resuelve `frontend/nginx.conf`, o la PWA se rompe en silencio:

1. **Sin `CustomErrorResponse` global que convierta 403/404 en `index.html` con 200.**
   El Service Worker recibiría HTML donde espera JSON/JS y se desactivaría solo.
2. **Behaviors dedicados con TTL 0** para `ngsw.json`, `ngsw-worker.js`, `safety-worker.js`,
   `manifest.webmanifest`, `version.json` e `index.html`.
3. Los assets con hash (`*.js`, `*.css`) sí pueden ir con TTL largo.
