# CI — Fijar el runner del deploy en `ubuntu-24.04` antes de que `ubuntu-latest` pase a Ubuntu 26

> Origen: el run `36446721389` de «Deploy to Production» (28-sep-2026) anotó *"The ubuntu-latest
> label will migrate to Ubuntu 26 beginning October 19, 2026"* (actions/runner-images#14748).

## 1. Diagnóstico (medido, no supuesto)

- **Hoy** `ubuntu-latest` resuelve a la imagen `ubuntu-24.04` (log del job: `Image: ubuntu-24.04`).
- **Anuncio** (#14748, publicado el 17-sep-2026): el label pasa a Ubuntu 26.04 de forma gradual entre
  el **19-oct y el 19-nov-2026**. La mitigación que indica GitHub es fijar el label `ubuntu-24.04`.
  No anuncia fecha de retiro de la imagen 24.04.
- **Lo que el workflow toma de la imagen** (ninguna action lo instala):

  | job | depende de la imagen |
  |---|---|
  | `tests` | Google Chrome en `/usr/bin/google-chrome` (`CHROME_BIN`, Karma `ChromeHeadless`) |
  | `deploy-backend` | docker (BuildKit), aws CLI, jq y el **node del sistema** (`node --test` de los scripts de TaskDef, sin `setup-node`) |
  | `deploy-frontend` | docker, aws CLI, jq y curl (gate del borde) |

- De eso, la tabla comparativa del anuncio solo lista Docker Buildx (0.37.0) y AWS CLI (2.36.40),
  iguales en las dos imágenes. **Chrome, node, jq y curl no figuran.**
- **Riesgo:** un día cualquiera entre el 19-oct y el 19-nov el gate de tests o un deploy se rompe sin
  que nadie haya tocado el repo, y el primer síntoma es un deploy de producción fallido.

## 2. Enfoque

- `runs-on: ubuntu-24.04` en los **3 jobs**. Es la imagen que ya corre ⇒ hoy no cambia nada.
- Comentario arriba del primer job con el porqué; el comentario del paso de Chrome deja de nombrar
  `ubuntu-latest`.
- Subir a Ubuntu 26 queda como **decisión deliberada**: commit propio, verificando antes lo que trae la
  imagen nueva (Chrome en `/usr/bin/google-chrome`, versión del node del sistema, docker) y probado en
  un run.

## 3. Archivos

- `.github/workflows/deploy-production.yml` — único workflow del repo (no hay otros con `ubuntu-latest`).
- Sin cambios de BD/SQL, backend ni frontend.

## 4. Reglas que NO se tocan (CLAUDE.md §🚀)

- Sin `dorny/paths-filter`; trigger `push` a `main-produccion` (+ `workflow_dispatch`).
- `wait-for-minutes: 25` en los dos deploys.
- Gate de tests intacto: mismos pasos, mismos `needs`/`if` en los deploys.

## 5. Casos de prueba

1. El YAML parsea (parser estricto, claves únicas) antes y después.
2. La única diferencia estructural son los 3 `runs-on` (`ubuntu-latest` → `ubuntu-24.04`).
3. Ningún `ubuntu-latest` en el YAML efectivo; la única mención es el comentario del porqué.
4. Invariantes: trigger, `wait-for-minutes` ≥ 25, `needs`, pasos del gate idénticos.
5. `JwtRotacionClaveCalculosTests` (único test que lee el workflow) sigue encontrando lo que busca.
6. **Prueba real: el próximo run de «Deploy to Production»** (merge a `main-produccion`). En el log de
   cada job tiene que verse `Image: ubuntu-24.04`, y el gate y los dos deploys en verde. Esta tarea no
   pushea ni despliega.

## 6. Pendiente (fuera de este cambio)

- Evaluar Ubuntu 26 en un run de prueba y subir en su propio commit, antes de que GitHub retire la
  imagen 24.04.
