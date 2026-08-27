# Plan — clasificar `guia-genetica-santa-reyes` en la lista cacheable (deploy 89511545875 cortado)

## Qué pasó

El run **89511545875** del workflow de producción falló en el job **«Tests — Backend & Frontend»**
con `exit code 1`, pero **no fue un test**:

| Paso | Resultado |
|---|---|
| 4 · Tests del backend | ✅ `Test Run Successful` — 3.453 / 3.453 |
| 7 · Tests del frontend | ✅ `TOTAL: 673 SUCCESS` |
| 8 · Gate `changeDetection` | ✅ 234 componentes, 0 sin estrategia |
| **9 · Gate lista cacheable** | ❌ `##[error]Process completed with exit code 1` |

```
[lista-cacheable]   sin decisión tomada : 1
[lista-cacheable]      - guia-genetica-santa-reyes  (features/config/guia-genetica-santa-reyes/guia-genetica-santa-reyes.service.ts)
```

El endpoint lo agregó `a34e7bb` (los tres módulos de guía genética separados) y nadie lo clasificó
en `decidir-cacheable.funcion.ts`. Es exactamente para lo que existe el gate: la lista es **blanca**
para que un controller nuevo no se cachee solo, y avisa al agregarlo en vez de descubrirse en una
tablet sin red. Mismo caso que `a41fa6e` (cuadre de alimento).

## Enfoque

Una línea en la lista + el test que fija la razón. No hay nada que implementar.

## La decisión: va a `EXCLUIDOS`

Sus dos hermanas **sí** se cachean, y la diferencia no es cosmética — es quién las lee:

| Endpoint | Lectores | Decisión |
|---|---|---|
| `guia-genetica` | `features/lote/services/guia-genetica.service.ts`, `services/guia-genetica.service.ts` (indicadores) | cacheable |
| `guia-genetica-ecuador` | `lote-engorde-list`, `indicadores-diarios-engorde-compute.service` | cacheable |
| `guia-genetica-santa-reyes` | **sólo** `pages/guia-genetica-santa-reyes-page` (`/config/...`) | **excluido** |

- La reducida no la pide ninguna pantalla de campo: `grep GuiaGeneticaSantaReyesService` devuelve un
  único consumidor, su propia pantalla de administración.
- Los indicadores de postura de Santa Reyes **no pasan por el front**: los calcula Postgres contra
  `vw_guia_genetica_postura` (`a278361`). Cachear el endpoint no los haría andar sin red.
- Es pantalla de oficina: crear, editar, dar de baja e importar exigen red y el permiso
  `guia_genetica.gestionar` + perfil de guía `reducida`.
- Mismo criterio ya escrito para `vacunacionplantilla`: es la fuente aguas arriba; servirla de caché
  mostraría una guía vieja como si fuera la vigente, justo antes de importarle encima.

**Reversible en una línea:** si operación decide que la pantalla tiene que consultarse sin red, se
mueve la cadena de `EXCLUIDOS` a `ENDPOINTS_OPERATIVOS` y el gate sigue verde.

## Archivos

- `frontend/src/app/shared/offline/funciones/decidir-cacheable.funcion.ts` — la entrada + su comentario.
- `frontend/src/app/shared/offline/funciones/decidir-cacheable.funcion.spec.ts` — test que fija la
  decisión **y** que las otras dos guías siguen cacheándose (la exclusión tiene que ser quirúrgica:
  las tres comparten prefijo `guia-genetica`, pero `extraerRecurso` compara el segmento completo).

## Riesgo de comportamiento: CERO

Al no estar en `ENDPOINTS_OPERATIVOS`, `decidirCacheable` **ya** devolvía `false` para esa ruta. El
cambio hace explícita la decisión, que es lo único que el gate exige.

## Casos de prueba

- `GET /api/guia-genetica-santa-reyes?page=1` → `false`
- `GET /api/guia-genetica-santa-reyes/plantilla` → `false`
- `GET /api/guia-genetica` → `true` (no se toca)
- `GET /api/guia-genetica-ecuador/anos` → `true` (no se toca)

## Verificación

- `node scripts/verificar-lista-cacheable.js` → 0 sin decisión, 0 fantasma.
- `node scripts/verificar-change-detection.js` → OK.
- `yarn test --include='**/decidir-cacheable.funcion.spec.ts'` → 12/12.
- `yarn build` → 0 errores.
