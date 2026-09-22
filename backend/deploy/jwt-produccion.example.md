# JWT de producción — clave nueva en cada deploy

> Plan y motivo: [`fase_de_desarrollo/jwt_firma_produccion_plan.md`](../../fase_de_desarrollo/jwt_firma_produccion_plan.md).

**No hay que hacer nada en AWS.** No usa Secrets Manager, no pide permisos nuevos ni cambios en la
consola: la rotación sale del pipeline de deploy, con los mismos permisos con los que ya cambia la
imagen en cada despliegue.

## Cómo funciona

El job del back de `.github/workflows/deploy-production.yml` ya hacía, en cada push a
`main-produccion`: bajar la task definition vigente → cambiarle la imagen → registrar una revisión
nueva → desplegarla. Entre el primer y el segundo paso ahora corre **«Rotar clave JWT en la TaskDef»**
(`backend/scripts/rotar-clave-jwt-taskdef.js`), que en el contenedor `backend`:

1. mueve la `JwtSettings__Key` que había a `JwtSettings__PreviousKey`;
2. pone en `JwtSettings__Key` 64 bytes aleatorios nuevos (88 caracteres base64);
3. enmascara las dos para el log de GitHub (`::add-mask::`) y solo informa largos.

Antes de rotar corre el test del script; si falla, no hay deploy.

La API **firma** con `JwtSettings__Key` y **valida** con las dos (`JwtRotacionClaveCalculos`). Resultado:
los tokens emitidos antes del deploy siguen valiendo hasta vencer (`JwtSettings__DurationInMinutes`,
hoy 60). **El deploy no desloguea a nadie.**

`appsettings.json` no participa en producción: la variable de la TaskDef lo pisa. Y si alguna vez la
TaskDef quedara sin `JwtSettings__Key`, la API se niega a arrancar con la clave de desarrollo que viaja
en la imagen, en vez de firmar con ella sin avisar.

## Qué esperar

- **Primer deploy con este cambio:** la clave que había (la misma desde hace meses, y copiada en claro
  en los snapshots `backend/deploy/*.json`) pasa a ser la anterior; en el deploy siguiente deja de
  valer del todo. Desde ahí, la clave filtrada está muerta.
- **Ventana del rollout (minutos):** mientras conviven tareas viejas y nuevas detrás del ALB, un token
  recién emitido por una tarea nueva puede caer en una vieja (que no conoce la clave nueva) y dar 401.
  A lo sumo algún usuario vuelve a iniciar sesión una vez.
- **Dos deploys en menos de 60 min:** la anterior cubre un solo paso; los tokens de dos claves atrás
  dejan de valer y esos usuarios vuelven a entrar.
- **Una `PreviousKey` que no sirve** (corta, igual a la actual o una clave del repo) se ignora: esos
  tokens no valen, pero la API arranca igual.
- **Deploys manuales** (`make deploy-backend`, `backend/deploy/*.ps1`) no rotan: usan la clave que tenga
  la task definition que registren.

## Rotar a demanda (por ejemplo, si se sospecha una filtración)

GitHub → Actions → **Deploy to Production** → *Run workflow* → marcar **Desplegar backend** → Run.
Es un deploy normal del back, así que rota la clave. Para que la filtrada deje de valer también como
anterior, correrlo **dos veces**; eso sí desloguea a todos.

## Cómo saber que rotó

En el log del job **Backend — Build & Deploy**, paso «Rotar clave JWT en la TaskDef»:

```
Clave JWT rotada: JwtSettings__Key nueva (88 caracteres); JwtSettings__PreviousKey = la anterior (… caracteres).
```

La primera vez que no hay clave previa dice `Clave JWT creada: …`. Si la task definition llegara a tomar
la clave de `secrets` (Secrets Manager), el paso avisa y no la toca.

## ⚠️ No versionar snapshots de la task definition

Los `.json` de `backend/deploy/` traen la clave en claro: así se filtró la anterior. No agregues
snapshots nuevos (`describe-task-definition > archivo.json`) al repo.
