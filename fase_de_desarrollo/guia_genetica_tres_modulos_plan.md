# Guía Genética — separación en TRES módulos con identidad propia

> **Decisión del usuario (26-ago-2026):** **no** se unifican las tres tablas. Son tres modelos de
> datos genuinamente distintos y se dejan separados **a propósito**. Lo que se corrige es que hoy
> hay **tres tablas y dos puertas, y ninguna abre la habitación correcta**: el menú de Santa Reyes
> apunta al módulo de engorde de Ecuador por herencia accidental, y `guia_genetica_santa_reyes`
> nació *seed-only*, sin un solo endpoint de escritura.
>
> **Estado destino — tres ítems de menú, uno por modelo:**
>
> | Ítem de menú | Ruta | Tabla | Empresas |
> |---|---|---|---|
> | **Guía Genética Pollo Engorde** | `/config/guia-genetica-ecuador` | `guia_genetica_ecuador_header/_detalle` | Ecuador + Panamá (engorde) |
> | **Guía Genética Sanmarino** | `/config/guia-genetica` | `guia_genetica_sanmarino_colombia` | Sanmarino / Demo (reproductora + postura) |
> | **Guía Genética Santa Reyes** | `/config/guia-genetica-santa-reyes` | `guia_genetica_santa_reyes` | Santa Reyes (postura) — **módulo nuevo** |

---

## 1. Enfoque arquitectónico

**Regla rectora: cero cambio de comportamiento fuera de Santa Reyes.** Todo lo que se construye es
**superficie nueva** sobre una tabla que hoy **nadie escribe** (verificado: `grep` de
`.Add|.Update|.Remove|SaveChanges` sobre `GuiaGeneticaSantaReyes` fuera de migraciones ⇒ vacío) y
que **ninguna función SQL lee** (`grep -rln "guia_genetica_santa_reyes" backend/sql/` ⇒ vacío).
Sanmarino, Demo, Ecuador y Panamá no pueden moverse porque no se toca una sola línea de sus caminos.

Las dos excepciones —los únicos dos puntos donde sí cambia comportamiento existente— son:

1. **El menú.** Se renombran dos ítems y se crea uno. Es lo que el usuario pidió explícitamente.
2. **`GuiaGeneticaService.ObtenerRazasCrudoAsync`** — hoy corta a nivel **empresa**, no de raza, y
   por eso el único workaround aparente falla en silencio (§4, caso F2.4). Se corrige **sólo** en el
   sentido de unir ambas fuentes cuando la empresa tiene guía propia; para toda empresa **sin** guía
   propia la salida es byte a byte la de hoy.

**Lo que NO entra en este trabajo** (queda declarado, con su motivo, en §7): que las 5 funciones SQL
de postura lean la guía de Santa Reyes. Es el *hueco de lectura*, tiene riesgo alto sobre funciones
compartidas con Sanmarino y exige un gate de paridad multipaís propio.

### Selección de módulo: por columna tipada, jamás por nombre de empresa

CLAUDE.md §🏢 prohíbe `if (empresa == 'Santa Reyes')`. La señal va en `companies` como columna
nombrada **por comportamiento**:

```sql
ALTER TABLE companies ADD COLUMN IF NOT EXISTS
  guia_genetica_perfil varchar(16) NOT NULL DEFAULT 'sanmarino';
-- valores: 'sanmarino' (tabla ancha compartida) | 'reducida' (tabla plana de 3 métricas)
```

El backfill se deriva de **datos**, no de nombre:
`UPDATE companies SET guia_genetica_perfil='reducida' WHERE EXISTS (SELECT 1 FROM guia_genetica_santa_reyes g WHERE g.company_id = companies.id)`.
Con eso, la empresa #4 que mañana quiera el modelo plano se da de alta cambiando **un dato**, no
desplegando código.

El flag gobierna: (a) el guard fail-closed del controller nuevo, (b) el guard fail-closed del
controller compartido, (c) qué ítem de menú se habilita, (d) qué pantalla ofrece el front.

---

## 2. Archivos a crear / modificar

### Backend — crear

| Archivo | Qué es |
|---|---|
| `Application/Calculos/GuiaGeneticaPerfilCalculos.cs` | `static class` pura: resuelve el perfil, **`throw` ante valor desconocido** (caer al default mostraría la tabla equivocada en silencio) |
| `Application/DTOs/GuiaGeneticaSantaReyesDtos.cs` | `GuiaGeneticaSantaReyesDto`, `Create…`, `Update…`, `…SearchRequest`, `…ImportResultDto` |
| `Application/Interfaces/IGuiaGeneticaSantaReyesService.cs` | Contrato CRUD + import |
| `Infrastructure/Services/GuiaGeneticaSantaReyes/GuiaGeneticaSantaReyesService.cs` | **partial ancla**: usings, campos, ctor, helpers, `: IGuiaGeneticaSantaReyesService` |
| `…/Funciones/GuiaGeneticaSantaReyesService.Crud.cs` | alta / edición / **soft delete** / búsqueda paginada |
| `…/Funciones/GuiaGeneticaSantaReyesService.Import.cs` | import Excel idempotente + plantilla |
| `API/Controllers/GuiaGeneticaSantaReyesController.cs` | `api/guia-genetica-santa-reyes` — GET/POST/PUT/DELETE + import + plantilla, con guard |
| `backend/tests/…/GuiaGeneticaPerfilCalculosTests.cs` | xUnit |
| `backend/tests/…/GuiaGeneticaSantaReyesCodigoTests.cs` | xUnit del código natural e idempotencia |
| 3 migraciones EF (§3) | flag, menús, permisos |

### Backend — modificar

| Archivo | Cambio |
|---|---|
| `Domain/Entities/Company.cs` | `+ GuiaGeneticaPerfil` |
| `Persistence/Configurations/CompanyConfiguration.cs` | mapeo de la columna |
| `Application/DTOs/CompanyDto.cs` | `+ guiaGeneticaPerfil` |
| **Las 4 proyecciones que siempre se olvidan**: `CompanyService.ToDto`, `CompanyService/Funciones/CompanyService.Crud.cs`, `CompanyResolver.cs`, `CompanyPaisService.cs` | propagar el campo — es el error exacto de la V52 (los flags no llegaron a `ActiveCompanyConfigService`) |
| `Infrastructure/Services/GuiaGeneticaService.cs:105` | `ObtenerRazasCrudoAsync`: unir propia + compartida cuando hay propia (§4 F2.4) |
| `API/Controllers/ProduccionAvicolaRawController.cs` | guard fail-closed: perfil `reducida` ⇒ `Forbid()` en **escritura** |
| `Program.cs` | DI del service nuevo |

### Frontend — crear

```
features/config/guia-genetica-santa-reyes/
├── models/guia-genetica-santa-reyes.model.ts
├── guia-genetica-santa-reyes.service.ts
├── funciones/                       # una acción por archivo, PURAS
│   ├── README.md
│   ├── construir-filas-tabla.funcion.ts
│   ├── exportar-guia-excel.funcion.ts
│   └── validar-fila-import.funcion.ts
└── pages/guia-genetica-santa-reyes-page/   # grid + form + modal import
```

### Frontend — modificar

`app.config.ts` (ruta nueva), `core/services/company-config/active-company-config.service.ts`
(exponer el perfil), y el menú/sidebar si tuviera etiquetas cableadas.

---

## 3. Cambios de BD / SQL — todo por migración, ninguno a mano

> ⚠️ **Las dos filas de `menus` actuales NO las creó ninguna migración** — viven sólo como espejo en
> `backend/sql/add_guia_genetica_menu.sql` y `add_guia_genetica_ecuador_menu.sql`, y alguien las
> corrió a mano en prod. Es exactamente el caso que la regla *«el `.sql` es el espejo, la migración
> es el vehículo»* existe para atrapar. **Consecuencia dura para este plan: el repo no puede probar
> qué filas de `menus` existen realmente en producción**, así que las migraciones de menú se
> escriben **defensivas** (localizar por `route`, `INSERT … WHERE NOT EXISTS`, `UPDATE` idempotente)
> y **nunca borran**: desactivan.

| # | Migración | Qué hace |
|---|---|---|
| 1 | `AddGuiaGeneticaPerfilCompany` | `ADD COLUMN IF NOT EXISTS guia_genetica_perfil` + backfill por datos |
| 2 | `SeedMenusGuiaGeneticaTresModulos` | renombra los dos ítems, crea el tercero, ordena, asigna `company_menus`/`role_menus` |
| 3 | `AddPermisoGuiaGeneticaSantaReyes` | permiso del módulo nuevo, **ON para todo rol que ya tenga el menú**, localizando por `route` (patrón anti-lockout) |

**Migración 2 — reglas duras:**

- `UPDATE menus SET label='Guía Genética Pollo Engorde' WHERE route='/config/guia-genetica-ecuador'`
  — corrige de paso las tildes que la `20260623080001` había quitado.
- `UPDATE menus SET label='Guía Genética Sanmarino' WHERE route='/config/guia-genetica'`.
- `INSERT` del ítem `/config/guia-genetica-santa-reyes` **`WHERE NOT EXISTS`**, y si las filas madre
  no existen (porque el `.sql` nunca corrió en ese entorno), **crearlas también** — así la migración
  deja el mismo estado final tanto en local como en prod, sin depender de lo que alguien corrió a mano.
- **Baja del ítem heredado**: a las empresas de perfil `reducida` se les pone `is_enabled=false` en
  `company_menus` para `/config/guia-genetica-ecuador` y `/config/guia-genetica`. **Desactivar, no
  borrar** — si el `INSERT` compensatorio no pegara, Ecuador y Panamá se quedarían sin ninguna pantalla.
- Localizar **siempre por `route`**, jamás por `id`: los ids difieren local ↔ prod.

**Espejos `.sql`** (`backend/sql/`) de lo que crea cada migración, para que el gate
`node backend/scripts/verificar-sql-llega-por-migracion.js` siga verde.

---

## 4. Reglas de negocio

**F2.1 — Idempotencia del import.** `codigo_guia_genetica = $"{Raza}{AnioGuia}{Edad}"` — misma
fórmula que `ExcelImportService.ComputeCodigo` (`ExcelImportService.cs:491-497`) y que el seed, contra
el UNIQUE parcial `ux_guia_genetica_santa_reyes_codigo` ya existente `(company_id, codigo_guia_genetica)
WHERE deleted_at IS NULL AND codigo_guia_genetica IS NOT NULL`. **Reimportar el mismo archivo
actualiza, no duplica** — a diferencia del import de la compartida, donde 644 de 1128 filas con
código NULL se reinsertan en silencio. El código **se recalcula** si cambia Raza/Año/Edad.

**F2.2 — Soft delete.** `DeletedAt`, nunca `Remove()`. El unique está filtrado por `deleted_at`
justamente para que dar de baja una línea no bloquee recrear el mismo código.

**F2.3 — Guard fail-closed en los dos sentidos.** Perfil `reducida` ⇒ `Forbid()` al escribir en
`ProduccionAvicolaRaw`; perfil `sanmarino` ⇒ `Forbid()` al escribir en la tabla reducida. Jamás
cae al otro perfil: hacer **inalcanzable** el estado malo es mejor que manejarlo.

**F2.4 — 🔴 `ObtenerRazasCrudoAsync` corta a nivel EMPRESA, no de raza.** Hoy
(`GuiaGeneticaService.cs:105-118`):

```csharp
var propias = await _ctx.GuiaGeneticaSantaReyes…ToListAsync();
if (propias.Count > 0) return propias;   // ← Santa Reyes SIEMPRE entra acá
```

Con 615 filas propias, ese método devuelve **siempre** las 5 razas sembradas: una raza nueva cargada
en la compartida se importa "OK", se lista en el grid y **nunca aparece en el selector de lotes**.
Se corrige uniendo ambas fuentes **sólo cuando hay propias**; para toda empresa sin guía propia la
rama es idéntica a la de hoy ⇒ **delta cero por construcción**.

**F2.5 — Cobertura visible.** La guía de Santa Reyes cubre semanas **18–140**; los reportes de
levante cubren 1–25. La pantalla lo dice, para que el usuario lo vea en vez de descubrirlo por un
reporte a medias.

**F2.6 — Raza como texto libre.** No un `<select>` alimentado por lo que ya existe: ese es el
*deadlock de arranque* que hoy vuelve inservible la pantalla de Ecuador (sin guía cargada no hay
raza que elegir ⇒ no se puede crear la primera).

**F2.7 — El alias de grafía no se toca.** `RazaGuiaAliasCalculos` tiene **dos** entradas y
`Lohmann Brown` está fuera **a propósito**: es una línea distinta de `Lohmann LSL` y no tiene guía
cargada. Ahora que hay pantalla, el cliente la carga él. Mapearla mostraría datos de otra ave.

---

## 5. Casos de prueba

**Backend (xUnit):**

1. `GuiaGeneticaPerfilCalculos`: `'sanmarino'` → compartida; `'reducida'` → propia; `null`/`""` →
   default `'sanmarino'`; **`'otro'` → `throw`**.
2. Código natural: `("Babcock Brown","2026",18)` ⇒ `"Babcock Brown202618"`; idéntico a
   `ExcelImportService.ComputeCodigo`; se recalcula al cambiar cualquiera de los 3.
3. Import idempotente: mismo archivo dos veces ⇒ mismo conteo, `updated` en la 2ª pasada.
4. Import con `prod_porcentaje` vacío ⇒ `NULL`, no `0` (la Criolla tiene 40 filas legítimamente
   nulas, semanas 101–140).
5. `ObtenerRazasCrudoAsync`: empresa **sin** guía propia ⇒ salida **byte a byte** la de hoy.
6. Guard: perfil `reducida` escribiendo en la compartida ⇒ `Forbid`, y viceversa.

**Verificación de delta cero (gate multipaís):** `backend/sql/verificar_paridad_guia_genetica.sql`
— hoy no existe; sin él «delta cero» es una afirmación, no una medición. Congela por empresa la
salida de los 8 objetos SQL vivos; 1ª corrida congela, 2ª compara. Exento del gate por prefijo
`verificar_*`. **Toda empresa que no sea Santa Reyes debe salir con 0 en todas las columnas.**

**Frontend:** `changeDetection: ChangeDetectionStrategy.Eager` **explícito** en los 4 componentes
nuevos — omitirlo en Angular 22 es OnPush y deja el modal colgado en «Cargando…» con el 200 ya en
Network. Probar **abriendo y cerrando el modal dos veces**. `ToastService` / `ConfirmDialogService`
(los métodos que confirman pasan a `async`), helpers de `shared/utils/excel/`. Prohibido `alert()`,
`confirm()`, `XLSX` inline salvo para **leer** el archivo subido.

**Smoke doble obligatorio:** empresa con perfil `sanmarino` (Sanmarino/Demo) ⇒ **cero cambios
visibles** salvo el rótulo del menú; Santa Reyes ⇒ 615 filas, alta, edición, baja e import.

---

## 6. Validación

```bash
cd backend && dotnet build        # 0 errores, sin advertencias nuevas
cd backend && dotnet test         # todo verde, + los tests nuevos
cd frontend && yarn build         # 0 errores; único warning aceptado: bundle budget preexistente
node backend/scripts/verificar-sql-llega-por-migracion.js   # verde
psql … -f backend/sql/verificar_paridad_guia_genetica.sql   # 1ª congela / 2ª compara ⇒ 0 fuera de Santa Reyes
```

Migraciones: probar `dotnet ef database update` en local **antes** de mergear. El deploy las aplica
solo (`Database__RunMigrations=true`).

---

## 7. Lo que este plan deja explícitamente afuera

**El hueco de LECTURA.** Los indicadores de postura los calcula Postgres:
`fn_indicadores_produccion_postura` y `fn_indicadores_levante_postura` leen
`guia_genetica_sanmarino_colombia` **hardcodeada**, y para Santa Reyes devuelven 0 filas ⇒ la columna
"Tabla" sale vacía, sin error. Los reportes técnicos en C# **sí** funcionan porque pasan por
`GuiaGeneticaLookup`. De ahí la sensación de *"a veces aparece y a veces no"*: depende de si la
pantalla la calcula C# o SQL.

El arreglo es una vista `vw_guia_genetica_postura` (`UNION ALL` de las dos tablas de postura) sobre
la que las 5 fns cambian **sólo** el `FROM`, sin tocar un `WHERE`. Para toda empresa sin guía
reducida el `UNION ALL` aporta **cero filas** ⇒ delta cero por construcción.

**No entra acá por tres razones:** (a) toca funciones compartidas con Sanmarino ⇒ exige el gate de
paridad completo corriendo antes y después; (b) hay un punto ciego sin resolver —
`RazaGuiaAliasCalculos` **sólo existe en C#** y las fns SQL comparan la raza exacta y
case-sensitive, y 3 de las 4 razas de los lotes reales de Santa Reyes no cruzan por grafía
(`BABCOK BROWN` vs `Babcock Brown`), así que sin resolver eso la vista entrega columnas vacías igual
que hoy; (c) es una decisión de alcance del usuario, no una consecuencia técnica de lo que pidió.

**Tampoco entra:** crear el UNIQUE que le falta a la tabla compartida (hoy 644 de 1128 filas tienen
`codigo_guia_genetica` NULL y el reimport las duplica en silencio — crear el UNIQUE convertiría ese
bug silencioso en una violación de constraint en el único camino real de carga), ni normalizar los
criterios de join de las fns (`lower(trim())`, `deleted_at`, `pais_id`), que hoy divergen a
propósito entre levante y producción: unificarlos haría que **empiecen a matchear filas que antes no
matcheaban**, o sea, el refactor cambiaría resultados por sí solo.
