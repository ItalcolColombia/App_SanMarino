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

## 5. Acción de datos en prod (la que resuelve el ticket)

Con el fix desplegado, la creación se hace **desde la UI** (Gestión de Granjas → Galpones → Nuevo
Galpón, granja NIZA III, núcleo Modulo IV, nombres `Galpon 1/2/3`). No hace falta SQL.

Antes de eso conviene confirmar el estado real de prod (la BD local es del 27jul26) con este SELECT
en DB Studio:

```sql
SELECT n.nucleo_id, n.nucleo_nombre, n.company_id, n.deleted_at,
       count(g.galpon_id) FILTER (WHERE g.deleted_at IS NULL) AS galpones_activos,
       count(g.galpon_id) FILTER (WHERE g.deleted_at IS NOT NULL) AS galpones_borrados
FROM nucleos n
LEFT JOIN galpones g ON g.nucleo_id = n.nucleo_id AND g.granja_id = n.granja_id
WHERE n.granja_id = 5
GROUP BY 1,2,3,4
ORDER BY 2;
```

- Si Modulo IV sale con `galpones_activos = 0` ⇒ es exactamente este caso: crear los 3 galpones.
- Si sale con galpones activos pero la UI no los muestra ⇒ mirar `company_id` de esos galpones
  (el filtro COMPAÑÍA del front descarta los de otra empresa) y `user_farm_scopes` del usuario.
