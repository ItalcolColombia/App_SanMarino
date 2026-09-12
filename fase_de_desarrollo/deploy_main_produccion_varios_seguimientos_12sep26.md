# Paso a `main-produccion` — varios seguimientos diarios por día (Santa Reyes)

Preparado el 12-sep-2026. **Nada pusheado ni desplegado todavía**: cada paso con efecto externo espera OK
explícito del usuario.

## 1. Qué viaja

8 commits, todos de esta serie (`origin/main` está en `f85994d`, así que primero hay que pushear `main`):

| Commit | Qué |
|---|---|
| `d7ee3df` | El flag por empresa funciona de verdad: alta/edición de producción y edición de levante leen el flag; índices únicos → triggers por fila (migración `20260912100000`) |
| `e84dd0f` | Grilla de producción con una fila por registro (`registrosDelDia`) |
| `ca23805` | Tracker |
| `efec417` | Diálogo de Eliminar con los datos del registro, no del día |
| `c41c571` | Tracker (validación end-to-end) |
| `ef63823` | `/resultado` de levante (500 en toda empresa desde may-2026) + reporte contable que perdía registros del día (migración `20260912130000`) |
| `218dad9` | Flag de Santa Reyes garantizado por migración (`20260912140000`) |
| `35abf15` | La grilla arma sus filas al asignar `seguimientos` (setter): arregla 5 specs de `tabs-principal` que el CI habría rechazado |

31 archivos, sin conflictos con `main-produccion` (`git merge-tree` limpio). El árbol de `origin/main-produccion`
(`ba34c65`) es idéntico al merge-base `f85994d`: la divergencia es solo de historia (merges de PR).

## 2. Migraciones que corren solas al arrancar la task nueva (`Database__RunMigrations=true`)

| Migración | Efecto en producción | Riesgo |
|---|---|---|
| `20260912100000_SeguimientoUnicoPorDiaSigueFlagEmpresa` | Borra `ux_seguimiento_diario_produccion_lote_dia_utc` y `ux_sdlr_tipo_lote_rep_dia_utc`; recrea `ix_seguimiento_diario_produccion_lote_id_fecha_registro` y `uq_sdlr_tipo_lote_rep_fecha` como NO únicos; crea 2 fns + 2 triggers BEFORE INSERT/UPDATE | Bajo. Tablas chicas (copia local: 1.176 y 608 filas) ⇒ el `CREATE INDEX` bloquea milisegundos. Idempotente. Probada 3 veces en clon con el pipeline real de EF |
| `20260912130000_ProduccionResultadoLevanteLoteIdTexto` | Convierte `produccion_resultado_levante.lote_id` a text **solo si no lo es** | Nulo: en prod ya es text ⇒ no-op |
| `20260912140000_SeedFlagMultiplesSeguimientosSantaReyes` | `UPDATE companies SET permite_multiples_seguimientos_diarios = true WHERE name = 'Santa Reyes' AND … IS DISTINCT FROM true` | Nulo: data-only, idempotente, por nombre |

Cambio de comportamiento visible para OTRAS empresas: **ninguno** con el flag apagado (tests + contraprueba 23505
en Sanmarino), salvo dos correcciones que aplican a todos:
- La ventana «Cálculos» de levante vuelve a responder y **recalcula** `produccion_resultado_levante` del lote al abrirse.
- El reporte contable suma los registros del mismo lote y día (en la copia local solo afecta 3 días de Demo, que
  podían mostrar mortalidad 0 por una fila de traslado).

## 3. Validación previa (hecha)

- `dotnet build` 0 err / 0 warn; Application.Tests 4.144/4.144; `yarn build` 0 errores; spec del front 8/8.
- 7 gates del CI en local: OK.
- CI equivalente sobre worktree limpio de `main` (solo lo commiteado): la primera corrida (`218dad9`) dio
  `dotnet test` Release OK y 7 gates OK, pero **`yarn test` 5 FAILED** en `tabs-principal.component.spec.ts`
  (regresión de `e84dd0f`; lo desplegado da 859/859 en la misma máquina). Corregido en `35abf15`; resultado de la
  suite completa sobre el nuevo `main` en el tracker, bloque D4. **Sin esta corrida, el pipeline habría cortado el
  deploy en `tests`.**
- Smoke por pantalla (Santa Reyes, clon) y ciclo completo por API con el flag apagado de entrada: alta, grilla,
  resultado, reporte, edición y borrado con reversión exacta de aves, stock por silo, huevos y fns.

## 4. Pasos (cada uno con OK explícito)

1. **Ventana**: horario de baja operación (el deploy recrea la task del backend; ~25 min de rollout).
2. `git push origin main` — no despliega nada.
3. `gh pr create --base main-produccion --head main --title "…" --body "…"` — mismo patrón que #99–#101. No despliega.
4. **Merge del PR** (merge commit, como #101) ⇒ push a `main-produccion` ⇒ GitHub Actions: tests + 7 gates ⇒
   `deploy-backend` (espera ECS hasta 25 min) ⇒ `deploy-frontend`.
5. Seguir el run: `gh run watch` sobre el run de `main-produccion`. Si falla `tests`, no se despliega nada.

## 5. Verificación post-deploy (obligatoria — ECS hace rollback silencioso)

```bash
aws ecs describe-services --cluster devSanmarinoZoo --services sanmarino-back-task-service-75khncfa --region us-east-2 \
  --query 'services[0].{TaskDef:taskDefinition,Running:runningCount,Deployments:deployments[].{Status:status,Rollout:rolloutState,TaskDef:taskDefinition}}'
aws ecs describe-task-definition --task-definition <arn-de-arriba> --region us-east-2 --query 'taskDefinition.containerDefinitions[0].image'
```
- La imagen debe llevar el tag del commit del merge. Si la TaskDef es la anterior ⇒ rollback silencioso: mirar
  `describe-services … events` (task started → deregistered = crash al migrar).
- Front: `version.json` servido por el ALB con el commit nuevo.

## 6. Smoke en producción (con el usuario)

- Empresas → Santa Reyes: «Varios seguimientos diarios el mismo día» encendido.
- Levante (Santa Reyes): abrir «Cálculos» de un lote ⇒ carga (antes quedaba vacío).
- Producción/levante (Santa Reyes): cargar el 2.º registro de un día real ⇒ la grilla muestra «2 registros» y
  «↳ 2.º del día»; el stock del silo baja lo cargado.
- Otra empresa (Sanmarino): intentar un 2.º registro del mismo día ⇒ rechazo «Ya existe un seguimiento…».
- Reporte contable de un lote con 2 registros en un día ⇒ suma ambos.

## 7. Rollback

- **Código**: redeploy de la TaskDef anterior (ECS) o PR con revert de los 7 commits.
- **Migraciones**: no se revierten solas. `20260912100000.Down()` restaura los índices únicos (fail-soft con
  WARNING si ya hay días duplicados de Santa Reyes); para aplicarlo en prod hace falta una migración nueva que lo
  ejecute. `20260912130000` y `20260912140000` no necesitan revertirse (no-op / flag).
- Si solo molesta el comportamiento en Santa Reyes: apagar el flag desde Empresas (efecto inmediato, sin deploy).
