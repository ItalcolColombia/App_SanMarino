# Núcleo 4 (Modulo IV) de NIZA III sin galpones — diagnóstico y corrección

**Reporte (19ago26, verenicemorales@sanmarino.com.co):** «Para crear lotes se requiere ubicar
galpones del núcleo 4 de la granja Niza 3, pero no aparecen. Fui al módulo, verifiqué y corregí
algunos datos, pero sigue mostrando solo 3 núcleos, tanto en la lista desplegable como en la lista
visible de detalle. Deben aparecer 3 galpones denominados 1, 2 y 3».

## 1. Qué se verificó (BD local, snapshot de prod del 27jul26)

| Hecho | Evidencia |
|---|---|
| El núcleo **existe**: `nucleo_id='543'`, `granja_id=5`, nombre `'Modulo IV -'`, `company_id=1`, `deleted_at NULL` | `select * from nucleos where granja_id=5` |
| El núcleo **no tiene un solo galpón** | `select ... from galpones where granja_id=5` → 13 galpones, repartidos en 233/323/324; ninguno en 543 |
| La granja 5 tiene 13 galpones; la pantalla del reporte muestra **10** | captura del ticket |
| `nucleo_id='543'` se repite en la granja 2 (`San maria Uno`, borrado 06nov25) — legítimo, la PK es `(nucleo_id, granja_id)` | `group by nucleo_id having count(*)>1` |

## 2. Por qué el núcleo «no sale»

Son **dos vistas distintas** y ninguna es un bug de datos:

1. **Tab Galpones (la de la captura):** el desplegable NÚCLEO **se deriva de los galpones cargados**,
   no del catálogo de núcleos — `galpon-list.component.ts:218-221` arma `nucleoMap` recorriendo
   `allGalpones`. Un núcleo **sin galpones nunca aparece ahí**, y tampoco tiene filas en la tabla.
   Por eso se ven solo Modulo I/II/III.
2. **Formulario de lotes:** los núcleos sí salen del catálogo (`nucleoSvc.getAll()`), pero los
   galpones se piden con `getByGranjaAndNucleo(granja, nucleo)` — para el 543 devuelve `[]`.

⇒ **El núcleo 4 está bien creado; lo que falta son sus 3 galpones.**

## 3. Por qué la usuaria no pudo crearlos (bug real)

`galpon-list.component.ts:400-406` propone el **ID del galpón nuevo** a partir del máximo de los
galpones **que el usuario ve**:

```ts
const lastNum = this.allGalpones.map(x => parseInt(x.galponId.replace(/\D/g,''),10))…reduce(max)
const newId = `G${(lastNum+1).toString().padStart(4,'0')}`;
```

Pero `galpones.galpon_id` es **PK global** (`galpones_pkey btree (galpon_id)`), no por empresa ni por
granja. Un usuario con alcance NIZA I + NIZA III ve como máximo **G0024** ⇒ el modal propone
**G0025**, que ya existe (`Galpon-2`, granja 37 SAN GUILLERMO, empresa 3). El backend rechaza
correctamente y explícito (`GalponService.CreateAsync`: *«Ya existe un galpón con el Id 'G0025'»*),
así que **el alta falla siempre** para cualquier usuario con alcance parcial. Un admin que ve las
534 filas obtiene G0535 y no lo sufre — de ahí que el problema solo lo reporte operación.

El backend ya tiene la resolución correcta (`GenerateNextGalponIdAsync`: recorre y **verifica
existencia global**), pero el front nunca la usa porque manda siempre un Id propuesto.

## 4. Enfoque de la corrección

**Principio:** el Id lo resuelve quien conoce todas las filas (el backend). El front deja de
inventarlo.

- **Backend** — `IGalponService.GetNextGalponIdAsync()` + `GET /api/Galpon/siguiente-id`, que expone
  el generador ya existente (`GenerateNextGalponIdAsync(effectiveCompanyId)`). Sin cambios de
  comportamiento en `CreateAsync`.
- **Frontend** — `GalponService.siguienteId()`; al abrir el modal en modo *crear*,
  `applyFormInModal` pide el Id al backend y lo pone en el form. Si la llamada falla, se cae al
  cálculo local actual (nunca deja el campo vacío) y el campo sigue siendo editable.
- **Sin tocar**: el desplegable derivado de galpones (es correcto: filtra lo que hay en la tabla) y
  el catálogo de núcleos.

### Archivos
- `backend/src/ZooSanMarino.Application/Interfaces/IGalponService.cs`
- `backend/src/ZooSanMarino.Infrastructure/Services/GalponService.cs`
- `backend/src/ZooSanMarino.API/Controllers/GalponController.cs`
- `frontend/src/app/features/galpon/services/galpon.service.ts`
- `frontend/src/app/features/galpon/components/galpon-list/galpon-list.component.ts`

### Casos de prueba
1. Usuario con alcance NIZA I+III abre «Nuevo Galpón» ⇒ el Id propuesto **no existe** en `galpones`.
2. Crear los 3 galpones en Modulo IV (granja 5, núcleo 543) ⇒ 201 y aparecen en la tabla; el
   desplegable NÚCLEO pasa a mostrar 4 opciones.
3. Editar un galpón existente ⇒ el Id sigue readonly y no se pide al backend.
4. Si `siguiente-id` falla (500/red) ⇒ el modal abre igual con el Id calculado localmente.

## 5. La corrección de datos viaja como migración EF (se aplica en el despliegue)

`20260820055219_SeedGalponesModuloIvNizaIii` — **data-only** (Designer clonado, ModelSnapshot
intacto). Crea `Galpon 1/2/3` en el núcleo Modulo IV de NIZA III. Como la TaskDef de ECS corre con
`Database__RunMigrations=true`, **el deploy la aplica solo**; no hace falta SQL a mano.

- **Identidad por nombre**, nunca por id fijo: empresa `Agroavicola Sanmarino` → granja `NIZA III` →
  núcleo `Modulo IV` (acepta la grafía vieja `Modulo IV -`).
- **Fail-open**: si el entorno no tiene esa granja/núcleo, `RAISE NOTICE` + `RETURN` — un seed no
  puede tumbar el arranque de la app.
- **Idempotente por partida doble**: no toca nada si el núcleo ya tiene 3 galpones activos (por si
  operación los crea a mano antes del deploy) y, por galpón, salta el que ya exista por nombre.
- **El Id se elige libre en ejecución** (máximo global `Gnnnn` + 1, avanzando si está ocupado), misma
  regla que `GalponService.GenerateNextGalponIdAsync`. Nada de ids hardcodeados.
- **Sin medidas inventadas**: `ancho`/`largo` NULL, `tipo_galpon = 'Abierto'` (el de los otros 13 de
  la granja), `created_by_user_id = 0` (marca de sistema).
- **`Down()`** borra solo los sembrados (`created_by_user_id = 0`) y **solo si siguen vacíos** (sin
  lotes/inventario/producción): revertir no puede llevarse por delante datos de negocio.
- Espejo en `backend/sql/crear_galpones_modulo_iv_niza_iii.sql` por si hay que correrlo en DB Studio
  antes del despliegue.

### Verificación de la migración (contra la copia de producción)
| Escenario | Resultado |
|---|---|
| Núcleo vacío | crea 3 (`NOTICE: 3 galpon(es) creado(s)`) |
| Segunda corrida | `NOTICE: ya tiene 3 galpon(es) activo(s); no se toca nada` — 0 filas |
| Falta 1 de 3 | crea solo ese, con id libre |
| Sin el núcleo (en transacción + ROLLBACK) | `NOTICE ... nada que hacer`, 0 inserts, sin excepción |
| `Down()` (en transacción + ROLLBACK) | borra los 3 sembrados; los 9 homónimos de Modulo I/II/III intactos |

## 5.1. Diagnóstico confirmado contra la copia de producción (20ago26)

La copia local ya está sincronizada con producción, así que el diagnóstico dejó de ser hipótesis:

| Núcleo | Galpones activos | |
|---|---|---|
| Modulo I (324) | 4 | |
| Modulo II (323) | 3 | |
| Modulo III (233) | 3 | +3 borrados |
| **Modulo IV (543)** | **0** | activo, empresa 1, `deleted_at` NULL |

Son los 10 registros de la captura del ticket. **No era el servicio ni permisos**:
`user_farms.restrict_locations = false` para la reportante, con NIZA III asignada.

La auditoría reconstruye la secuencia del **18ago26**: 12:50–12:52 se renombraron los galpones de
Modulo II y III a `1/2/3`; **12:56** se borraron `G0020/G0021/G0022` (`Galpon 11/12/13` de Modulo III),
que no tienen **ni una** fila dependiente. Los de Modulo IV nunca se pudieron crear: con el alcance de
la reportante (54 granjas de la empresa 1) el modal proponía **`G0443`** — `galpon pruebas`, granja 44
*Pruebas Moises*, que ella no ve — y el alta se rechazaba por Id ocupado.

Verificado también que la consulta que emula `GET /api/Galpon` con su alcance devuelve **13 filas con
Modulo IV incluido** una vez creados los galpones ⇒ el desplegable pasa a 4 núcleos y el formulario de
lotes ofrece los 3 galpones.
