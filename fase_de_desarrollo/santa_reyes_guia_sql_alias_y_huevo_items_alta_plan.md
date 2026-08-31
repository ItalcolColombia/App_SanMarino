# Santa Reyes — la guía genética en el camino SQL + los tipos de huevo al ALTA del lote

**Pedido (30-ago-2026):** verificar que producción, levante y gestión de lotes estén realmente
conectados con la guía genética de Santa Reyes, y que los tipos de huevo que se declaran **al crear
el lote** sean los que aparecen en la fase de producción.

---

## 1. Auditoría previa (medida, no deducida)

Todo medido contra la copia local `sanmarinoapplocal:5433` (company 6 = Santa Reyes, 1 lote: 152
`LOTE 218A`, raza `Criolla` 2026) y con smokes en transacción con `ROLLBACK`.

### Lo que YA funciona — no se toca

| Punto | Evidencia |
|---|---|
| `guia_genetica_santa_reyes` | 5 razas × 123 semanas, edad 18→140, company 6 |
| Indicadores de producción leen la guía propia | smoke lote 152 sem. 30 → prod guía **72,87 %**, consumo **113,00**, retiro **1,10** |
| Indicadores de levante idem, sin promediar por 2 ni coalescear | smoke sem. 20 → consumo guía **107,00**; peso/unif/mort **vacíos** (la guía reducida no los trae) |
| Selector de raza/año del alta de lote | `LoteFormDataService` → `ObtenerRazasCrudoAsync` (une propia + compartida) |
| Validación (raza, año) al crear/editar lote | `LoteService.Crud` → `GuiaGeneticaLookup.ExisteAsync` (con alias ERP) |
| Ítems de huevo por lote — backend | `lote_huevo_items` + `LoteHuevoItemService` (empresa por `farms.company_id`) + gate `ValidarHuevoItemsAsync` fail-closed |
| El diario de producción pide los ítems con el id correcto | `[loteId]="selectedLote?.loteId"` (maestro, no el espejo `lpp`) |

### Los 4 defectos a corregir

| # | Defecto | Medición |
|---|---|---|
| **G1** | El alias de grafía del ERP vive **sólo en C#** (`RazaGuiaAliasCalculos`); las fns/vistas SQL comparan la raza cruda | `BABCOK BROWN` / `HY LINE` → producción **vacío**, levante **0,00**; el reporte técnico (C#) sí muestra la guía ⇒ el mismo lote da distinto según la pantalla |
| **G2** | `fn_indicadores_levante_postura` compara la raza **case-sensitive** (`g.raza = v_raza`) | `CRIOLLA` cruza en producción (72,87 %) y **no** en levante (0,00) |
| **G3** | En levante, no cruzar se pinta **`0,00`**, no vacío: el `COALESCE(...,0)` corre cuando `origen IS NULL` (sin fila), no sólo con la compartida | smoke `CRIOLLA` → consumo/peso/mort/unif de guía en `0,00`: un objetivo falso |
| **G4** | `fn_indicadores_produccion_postura` descarta toda semana de vida **< 25** (`DELETE FROM _seg WHERE sem_vida < 25` + `FOR s IN 25..`) | Santa Reyes tiene guía desde la **18** y `huevo_primera_postura_hasta_semana = 22` ⇒ sus semanas 18–24 no aparecen. Smoke a semana 22: **0 filas**; a semana 30: 1 fila con guía |
| **H1** | Los tipos de huevo del lote sólo se declaran **después** de crear el lote (botón 🥚 por fila en la lista); el formulario de alta no los ofrece | `lote-list.component.html:501` y `:1575` |

**Estado del dato hoy (local):** el lote 152 tiene **0 tipos declarados** ⇒ fail-closed: en producción
no le aparece ninguna fila de huevo. El catálogo de Santa Reyes tiene **28 ítems de huevo activos**.

---

## 2. Enfoque arquitectónico

### Regla que gobierna las 3 partes: **delta cero por construcción, no por revisión**

Las 4 empresas restantes (Sanmarino, Demo, Ecuador, Panamá) no tienen guía propia
(`guia_genetica_santa_reyes` sólo tiene filas de company 6; las dos tablas están particionadas por
empresa). Cada cambio se acota a la rama `origen = 'propia'` o a un flag con **DEFAULT neutro**, de
modo que para ellas la expresión ejecutada sea **literalmente la de hoy**.

⚠️ **Lo que NO se hace:** unificar los criterios de join entre levante y producción. Divergen a
propósito (levante compara exacto y no filtra `deleted_at`; producción usa `btrim(lower())` y sí
filtra). Unificarlos haría matchear filas que hoy no matchean ⇒ el refactor cambiaría resultados por
sí solo. Sólo se agrega una rama nueva para la guía propia.

### Parte A — el alias de raza en SQL (G1 + G2 + G3)

Nueva fn pura `fn_raza_guia_alias(text) RETURNS text`, espejo exacto de
`RazaGuiaAliasCalculos.AliasGuiaPropia`: normaliza (`btrim` + `lower`) y mapea `babcok brown` →
`babcock brown`, `hy line` → `hy line brown`. `Lohmann Brown` **no** se mapea (es otra línea
comercial y no tiene guía cargada: mapearla mostraría datos de un ave que no es esa).

Los 4 objetos que consultan la guía cambian **sólo la rama propia**:

| Objeto | Hoy | Queda |
|---|---|---|
| `fn_indicadores_produccion_postura` | `btrim(lower(g.raza)) = btrim(lower(v_raza))` | `CASE WHEN g.origen='propia' THEN btrim(lower(g.raza)) = fn_raza_guia_alias(v_raza) ELSE <expresión de hoy> END` |
| `fn_indicadores_levante_postura` | `g.raza = v_raza` (exacto) | `(g.origen='propia' AND btrim(lower(g.raza)) = fn_raza_guia_alias(v_raza)) OR (g.origen<>'propia' AND g.raza = v_raza)` |
| `fn_resumen_semanal_ra_pesadas_levante` / `_produccion` | `lower(trim(gg.raza)) = lower(trim(so.raza))` | idem con `CASE ... origen='propia'` |
| `vw_guia_genetica_por_lote_postura` | `gu.raza = l.raza::text` | idem con `CASE ... origen='propia'` |

**G3** se corrige acotado: el `COALESCE(...,0)` de levante se aplica hoy cuando `v_origen_guia IS
DISTINCT FROM 'propia'`, lo que incluye el caso «no hubo fila» (`NULL`). Pasa a no aplicarse cuando
**la empresa tiene guía propia** (`EXISTS` sobre `guia_genetica_santa_reyes` por `company_id`): ahí un
0 sería un objetivo inventado y NULL es la única lectura honesta. Para las otras 4 empresas el
`EXISTS` es falso ⇒ expresión idéntica a la de hoy.

### Parte B — la semana de arranque de producción, por empresa (G4)

Columna nueva `companies.semana_inicio_indicadores_produccion int NOT NULL DEFAULT 25`, nombrada por
el **comportamiento** (no por el tenant), con el default = valor de hoy. Seed: Santa Reyes = **18**
(decisión del usuario, 30-ago-2026: es la primera edad de su guía y es coherente con
`huevo_primera_postura_hasta_semana = 22`).

`fn_indicadores_produccion_postura` resuelve el número una vez (`SELECT ... FROM companies WHERE id =
p_company_id`, `COALESCE(..., 25)`) y lo usa en el `DELETE` y en el `FOR`. Con DEFAULT 25 las otras
empresas ejecutan exactamente lo mismo que hoy.

### Parte C — los tipos de huevo en el ALTA del lote (H1)

El lote no existe hasta el POST, así que el alta no puede usar `GET /LoteHuevoItem/{loteId}/disponibles`.
Se agrega el gemelo por granja, que resuelve la empresa por el **mismo dato** que el gate de guardado
(`farms.company_id`, nunca la empresa activa del token):

```
GET /api/LoteHuevoItem/por-granja/{granjaId}/disponibles
```

En `lote-list` (el formulario vivo de lotes), con el flag `clasificacionHuevoPorItems` ON:

1. Sección nueva en el modal de crear/editar, alimentada por ese endpoint al elegir la granja.
2. En **edición** se precargan los ya declarados (`GET /LoteHuevoItem/{loteId}`).
3. Al guardar: POST/PUT del lote y, con el `loteId` de la respuesta, `PUT /LoteHuevoItem/{loteId}`.
4. Si el lote se crea pero el PUT de ítems falla, el toast lo dice explícitamente («el lote se creó;
   los tipos de huevo no se guardaron, usá el botón 🥚») — nunca un éxito silencioso.
5. El botón 🥚 de la lista **se conserva** (editar los tipos sin abrir el lote entero).

---

## 3. Archivos

**Backend — SQL/migraciones**
- `backend/sql/fn_raza_guia_alias.sql` *(nuevo, espejo)*
- `backend/sql/fn_indicadores_produccion_postura.sql` · `fn_indicadores_levante_postura.sql` ·
  `fn_resumen_semanal_ra_pesadas_levante.sql` · `fn_resumen_semanal_ra_pesadas_produccion.sql` ·
  `vw_guia_genetica_por_lote_postura.sql` *(espejos actualizados)*
- `Migrations/<ts>_AliasRazaGuiaEnSqlYSemanaInicioProduccion.cs` *(+ `.Designer.cs`, `.Sql.cs`)* —
  el **vehículo**: crea la fn, recrea los 5 objetos, agrega la columna a `companies` y siembra el 18
  para Santa Reyes. Idempotente (`CREATE OR REPLACE`, `ADD COLUMN IF NOT EXISTS`,
  `UPDATE ... WHERE ... IS DISTINCT FROM`).

**Backend — C#**
- `Domain/Entities/Company.cs` + `Persistence/Configurations/CompanyConfiguration.cs` — la columna
- `Application/DTOs/CompanyDtos.cs` + las 4 proyecciones (`CompanyService.ToDto`, `CompanyService.Crud`,
  `CompanyResolver`, `CompanyPaisService`) — el flag viaja al front
- `Application/Interfaces/ILoteHuevoItemService.cs` + `Services/LoteHuevoItemService.cs` +
  `API/Controllers/LoteHuevoItemController.cs` — `GetDisponiblesPorGranjaAsync`

**Frontend**
- `features/lote/services/lote-huevo-items.service.ts` — `getDisponiblesPorGranja`
- `features/lote/components/lote-list/lote-list.component.{ts,html}` — la sección en el alta/edición
- `features/lote/funciones/huevo-items-seleccion.funcion.ts` *(nuevo, puro)* + su `.spec.ts`

**Tests**
- `backend/tests/.../RazaGuiaAliasCalculosTests.cs` — se extiende con la paridad C# ↔ SQL
- `backend/tests/.../SemanaInicioIndicadoresProduccionCalculosTests.cs` *(nuevo)*

---

## 4. Casos de prueba

**A · alias en SQL** (smoke en transacción con `ROLLBACK` sobre el lote 152)

| raza del lote | producción hoy | producción esperado | levante hoy | levante esperado |
|---|---|---|---|---|
| `Criolla` | 72,87 % | 72,87 % (sin cambios) | 107,00 | 107,00 |
| `CRIOLLA` | 72,87 % | 72,87 % | **0,00** | **107,00** |
| `Babcock Brown` | 95,80 % | 95,80 % | 107,00 | 107,00 |
| `BABCOK BROWN` | **vacío** | **95,80 %** | **0,00** | **107,00** |
| `HY LINE` | **vacío** | valor de `Hy Line Brown` | 0,00 | valor de `Hy Line Brown` |
| `Lohmann Brown` | vacío | **vacío** (no se inventa guía) | 0,00 | **vacío** |

**Gate multipaís (obligatorio, CLAUDE.md):** `backend/sql/verificar_paridad_guia_genetica.sql` antes y
después. Toda empresa ≠ Santa Reyes debe salir en **0 en todas las columnas**.

**B · semana de arranque**
- Santa Reyes, lote en semana 22 → hoy **0 filas**; esperado: la fila de la semana 22 con su guía.
- Sanmarino/Demo, cualquier lote → mismas semanas y mismos números que antes del cambio.

**C · tipos de huevo en el alta**
- Empresa con flag OFF (Sanmarino/Demo) → el formulario de lote no muestra la sección: **cero cambios**.
- Santa Reyes, alta con 3 ítems tildados → el lote nace con 3 filas en `lote_huevo_items` y el diario
  de producción pinta esas 3 filas fijas y ninguna más.
- Alta sin tildar ninguno → se crea sin ítems y el diario muestra el mensaje accionable (fail-closed,
  decisión del cliente del 21-ago-2026).
- Edición: destildar uno → `activo = false`, y los seguimientos ya guardados conservan su desglose
  (cada uno guarda su foto en `metadata.huevoItems`).
- El PUT de ítems falla tras crear el lote → toast que lo dice; el lote queda creado.

**Validación:** `dotnet build` + `dotnet test` (backend) · `yarn build` (front) · smoke doble
(empresa con flag OFF y Santa Reyes).
