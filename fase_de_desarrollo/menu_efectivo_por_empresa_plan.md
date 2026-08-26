# El menú del usuario tiene que respetar lo que la empresa tiene habilitado

> Reportado el 26-ago-2026 sobre **ItalcolPanamá**: el sidebar muestra módulos (ItalJira: Backlog,
> Tablero, Roadmap, Panel de control) que **no están asignados a la empresa**. Quitarlos de la
> empresa no cambió nada, porque el menú efectivo nunca mira esa tabla.

---

## 1. El defecto, medido

`RoleCompositeService.Menus_GetForUserAsync` — lo que alimenta `GET /api/Roles/menus/me`,
`GET /api/Auth/menu` y el `SessionBootstrapDto` del login, o sea **todo el sidebar** — arma el menú
así:

```
role_menus (roles del usuario)  ∩  menus.is_active  ∩  menu_permissions
+ ancestros de cada ítem permitido
```

**`company_menus` no aparece en ninguna parte.** Esa tabla la lee un solo servicio,
`CompanyMenuService`, que alimenta la pantalla de administración «Menús por empresa». O sea: el
switch existe, la UI de administración existe, y **al runtime no le llega**.

Medido sobre la copia local de producción — pares (empresa, menú) visibles por rol que la empresa
**no** tiene habilitados:

| Empresa | Menús que se cuelan | Usuarios afectados | Pares (usuario, menú) |
|---|---:|---:|---:|
| ItalcolPanamá | 7 | 4 | 28 |
| ItalcolEcuador | 4 | 6 | 15 |
| Agroavicola Sanmarino | 2 | 2 | 3 |
| Demo | 1 | 3 | 3 |
| Santa Reyes | 1 | 2 | 2 |
| **Total** | | | **51** |

Los 15 ítems, uno por uno (los 51 pares son estos por los usuarios que los ven):

| Empresa | Menú | Ruta |
|---|---|---|
| Sanmarino | db_studio | `/config/db-studio` |
| Sanmarino | Historial de Inventario | `/gestion-inventario/historial` |
| Demo | Reporte Diario Costos Postura | `/reporte-diario-costos-postura` |
| Ecuador | Guía Genética | `/config/guia-genetica` |
| Ecuador | Movimientos | *(grupo, sin ruta)* |
| Ecuador | Historial de Inventario | `/gestion-inventario/historial` |
| Ecuador | ItalJira | *(grupo; sus hijos ya los tapaba el filtro de permisos)* |
| **Panamá** | **Guía Genética** | `/config/guia-genetica` |
| **Panamá** | **Bandeja de gestión** | `/tickets/gestion` |
| **Panamá** | **ItalJira + Backlog + Tablero + Roadmap + Panel de control** | `/italjira/*` |
| Santa Reyes | Reporte Diario Costos Postura | `/reporte-diario-costos-postura` |

**No es deuda vieja de la UI de asignación:** el modal de rol **ya** ofrece únicamente los menús de
la empresa (`role-management.component.ts` llama `getMenusForCompany`). Las filas de `role_menus`
que sobran son residuos de antes de ese cambio y de seeds por migración. Falta el gate de runtime.

Es exactamente el eje que `company_permissions` ya resolvió para permisos (manda en asignación **y**
en runtime); a `company_menus` le falta la mitad de runtime.

---

## 2. Enfoque: la decisión se resuelve en la BD, en una sola llamada

Pedido explícito del usuario: *«esto es más de la base de datos, deberíamos pasar a la base de datos
por una función que retorne todo construido para reducir consumo o demora en el back para algo que
conecta tablas entre sí»*.

Hoy el método hace **4 round-trips** a Postgres (roles → keys de permiso → catálogo de menús con su
subquery de `menu_permissions` → menús asignados) y arma el árbol en memoria del backend. Agregar el
filtro por empresa lo llevaría a 5.

Pasa a **una sola llamada** a `fn_menu_usuario(p_user_id uuid, p_company_id int)` que devuelve el
árbol **ya construido** como `jsonb`. El backend deserializa a `MenuItemDto[]` y responde. Toda la
relación entre las 6 tablas (`user_roles`, `role_menus`, `role_permissions`, `permissions`, `menus`,
`menu_permissions`, `company_menus`) se resuelve donde viven los índices.

### La regla, completa

```
visibles =
    menus.is_active
  ∩ ( role_menus(roles del usuario en la empresa)          -- si el usuario tiene alguno
      | menús sin permisos requeridos o con uno que tenga  -- fallback, sin role_menus
    )
  ∩ menu_permissions ⊆ permisos del usuario (o sin requisitos)
  ∩ HABILITADOS PARA LA EMPRESA                             ← lo nuevo
+ ancestros de cada visible
```

### Cuatro decisiones, con su razón

**D1 — «habilitado para la empresa» = fila en `company_menus` con `is_enabled = true`.** Tanto la
fila ausente como `is_enabled = false` ocultan. Es lo que la pantalla de administración escribe.

**D2 — Empresa SIN ninguna fila en `company_menus` ⇒ no se filtra (fail-open por empresa).**
`CompanyService.CreateAsync` siembra el catálogo completo de `company_permissions` pero **no siembra
`company_menus`**. Con fail-closed, la primera empresa nueva nacería con el menú **vacío** y sin
forma de arreglarlo desde la app: para asignar menús hay que entrar a Configuración → Empresas, que
es justamente un ítem del menú que no se vería. Es la misma trampa que la memoria de
`company_permissions` señala («lo delicado no es el gate, es el seed»), y acá no hay seed. Fail-open
sobre la tabla vacía **no puede empeorar lo de hoy** (hoy no se filtra nunca) y deja el gate activo
en las 5 empresas reales, que sí tienen filas (46/24/23/25/34).

**D3 — Los ancestros se incluyen solos.** Un grupo padre que no esté en `company_menus` pero con
hijos habilitados se muestra igual, porque si no el submenú entero desaparece. Es exactamente lo que
ya hace el filtro por `role_menus`; no se introduce un criterio nuevo.

**D4 — El orden y la jerarquía siguen saliendo de `menus`, no de `company_menus`.** La tabla tiene
`sort_order` y `parent_menu_id` por empresa y hoy **el sidebar los ignora**: usar de golpe el orden
por empresa reordenaría los menús de las 5 empresas en el mismo commit que arregla la visibilidad, y
eso ya no sería este arreglo. Queda fuera de alcance, anotado.

### Empresa efectiva: por dato validado, nunca por el header crudo

`companyId` llega como query param opcional y **el sidebar no lo manda** (`ensureLoaded()` sin
argumentos). Hoy eso significa «todos los roles del usuario en todas sus empresas», que es parte del
problema. Los tres endpoints pasan a caer a `_currentUser.CompanyId`, que es el que
`ActiveCompanyMiddleware` ya validó contra `UserCompanies` (o super admin). Nunca el header crudo.

---

## 3. Archivos

**Backend — nuevo:**
- `backend/sql/fn_menu_usuario.sql` — espejo legible de la función.
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260826120000_FnMenuUsuario.cs` (+ `.Fn.cs`
  con la constante SQL + `.Designer.cs` clonado del snapshot) — **el vehículo**. Sin cambios de
  modelo: `ZooSanMarinoContextModelSnapshot.cs` no se toca.
- `backend/src/ZooSanMarino.Application/Calculos/MenuVisibilidadCalculos.cs` — la regla como lógica
  pura. Es la **especificación ejecutable** de la función (patrón `SeguimientoAvesEngordeCalculos`):
  en runtime manda el SQL, en tests manda esto.
- `backend/tests/ZooSanMarino.Application.Tests/MenuVisibilidadCalculosTests.cs`.
- `backend/sql/verificar_menu_usuario_paridad.sql` — diagnóstico de solo lectura: reproduce en SQL la
  regla **vieja** y la diffea contra la fn nueva, usuario por usuario y empresa por empresa.

**Backend — modificado:**
- `RoleCompositeService.cs` → `Menus_GetForUserAsync` delega en la fn (1 llamada) y deserializa.
- `RoleController.cs` → `menus/me` y `menus/user/{id}` caen a `_currentUser.CompanyId` si no viene
  `companyId`.
- `AuthController.cs` → `GET /api/Auth/menu` idem.

**Frontend:** ninguno. El contrato (`MenuItemDto[]`) no cambia.

---

## 4. Casos de prueba

`MenuVisibilidadCalculosTests` (xUnit, gate de CI):

1. **Flag apagado por ausencia de datos** — empresa sin filas en `company_menus` ⇒ el resultado es
   **idéntico** al de hoy, ítem por ítem y en el mismo orden.
2. Menú asignado al rol pero **ausente** de `company_menus` ⇒ no aparece.
3. Menú asignado al rol y presente con `is_enabled = false` ⇒ no aparece.
4. Menú asignado al rol y presente con `is_enabled = true` ⇒ aparece.
5. **Ancestro no habilitado, hijo sí** ⇒ aparecen los dos (D3).
6. **Padre habilitado, ningún hijo visible** ⇒ el padre aparece vacío (es lo que hace hoy).
7. Menú con `menu_permissions` que el usuario no tiene ⇒ no aparece aunque la empresa lo habilite
   (el gate de permisos no se afloja).
8. Rama fallback (usuario **sin** `role_menus`) ⇒ el filtro por empresa también aplica.
9. Cadena rota por un ancestro `is_active = false` ⇒ el nodo se descarta (comportamiento actual).
10. Empate de `order` ⇒ orden determinista por `id` (hoy queda al azar del motor).
11. `p_company_id` nulo ⇒ sin filtro por empresa (endpoint de administración por usuario).

**Paridad en BD** (`verificar_menu_usuario_paridad.sql`), sobre la copia de producción:
- Con el filtro de empresa **neutralizado**, la fn tiene que dar **0 diferencias** contra la regla
  vieja para los 49 usuarios × sus empresas. Eso prueba que el port a SQL no cambió nada.
- Con el filtro **activo**, la única diferencia admitida son los 51 pares de la tabla de §1.

**Smoke HTTP:** login como usuario de Panamá → `GET /api/Roles/menus/me` sin `companyId` → el árbol
no trae `/italjira/*` ni `/config/guia-genetica` ni `/tickets/gestion`; el resto queda igual.

---

## 5. Lo que este cambio hace desaparecer, y que hay que mirar antes de mergear

El arreglo hace que `company_menus` mande de verdad, así que **los 51 pares de §1 dejan de verse**.
En Panamá eso es exactamente lo pedido. En las otras cuatro empresas hay ítems que probablemente se
usan (`db_studio` y `Historial de Inventario` en Sanmarino, `Reporte Diario Costos Postura` en Demo y
Santa Reyes).

**No se agrega ningún backfill.** Reponerlos es asignarlos desde Configuración → Empresas → Menús,
que es la pantalla que existe para eso y que a partir de este cambio por fin tiene efecto. Un
backfill masivo dejaría todo como está y volvería el arreglo invisible. La lista queda arriba para
que la decisión sea explícita y no una sorpresa post-deploy.

---

## 6. Validación

```bash
dotnet build     # backend, 0 errores
dotnet test      # incluye MenuVisibilidadCalculosTests
node backend/scripts/verificar-sql-llega-por-migracion.js   # el fn_ tiene su migración
psql ... -f backend/sql/verificar_menu_usuario_paridad.sql  # 0 diferencias fuera de las esperadas
```

El front no se toca ⇒ no hace falta `yarn build`, pero se corre igual el smoke del sidebar.
