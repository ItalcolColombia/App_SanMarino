# Plan — Dejar la empresa **Demo** lista para que el equipo de costos practique carga masiva

> **Origen.** El equipo de costos de SanMarino tiene que aprender a armar los archivos de carga
> masiva, subirlos contra las granjas, validar la información y contrastar los reportes de costos
> **antes** de operar sobre la empresa real. Hoy no puede: en Demo el módulo de carga masiva y el
> reporte de costos **no existen en pantalla**.
>
> Decisiones tomadas por el usuario al abrir la tarea:
> 1. **Alcance:** solo lo que usa costos (no espejo completo de SanMarino).
> 2. **Destino:** migración EF probada y commiteada, **sin desplegar** hasta OK explícito.
> 3. **Datos:** limpiar los datos operativos de Demo y **dejar solo la estructura**.

---

## 1. Auditoría (BD local `sanmarinoapplocal`, copia de producción, 28-ago-2026)

### 1.1 El hallazgo: la cadena está cortada en **cuatro niveles**, no en uno

Arreglar solo el menú no alcanza. Medido ejecutando `fn_menu_usuario` con el usuario real
`admin.demo@zootecnico.com` (`29246824-…`) sobre la empresa 4:

```
 grupo                 | hijos visibles
-----------------------+-------------------------------------------------------
 Carga Masiva          | (SIN HIJOS)          ← el grupo se pinta VACÍO
 Reportes              | Reporte Técnico Sanmarino | Reporte Contable
```

| # | Nivel | Qué falta en Demo | Efecto para el equipo de costos |
|---|---|---|---|
| 1 | `company_menus` | menú `migraciones_masivas` (`/migraciones-masivas`) | El grupo **Carga Masiva** aparece **vacío**: no hay dónde hacer clic. |
| 2 | `company_menus` | menú `reporte_diario_costos_postura` + `reporte_tecnico_semanal` | El reporte de costos **no aparece** aunque el rol ya lo tenga asignado. |
| 3 | `company_permissions` | permiso `carga_masiva_postura` | Ni siquiera un admin de Demo puede **otorgarlo**: no está en el catálogo de la empresa. |
| 4 | `role_permissions` / `role_menus` | `carga_masiva_postura` y el menú hijo en los roles 23/24 | Con el menú visible, los tiles de Postura saldrían **grises**: *«Sin permiso para carga masiva»*. |

**Detalle que confirma el diagnóstico:** los dos roles de Demo (`Admin Demo`, `Usuario pruebas`)
**ya tienen** en `role_menus` el grupo `carga_masiva` y el `reporte_diario_costos_postura`. La
configuración de rol estaba lista; lo que nunca se habilitó fue la **empresa**. Por eso el síntoma es
un grupo de menú vacío y no un 403.

**Contrasentido que hay que corregir:** `Admin Demo` tiene `carga_masiva_pollo_engorde` y **no**
`carga_masiva_postura` — el permiso justo al revés de lo que Demo necesita. Demo no tiene un solo
lote de engorde; toda su operación es levante + producción.

### 1.2 Los flags de comportamiento divergen — el reporte leería otra fuente

`companies` (empresa 1 = Agroavicola Sanmarino, empresa 4 = Demo):

| flag | SanMarino | Demo | Consecuencia si no se alinea |
|---|---|---|---|
| `reportes_alimento_desde_inventario_unificado` | **true** | false | 🔴 El Contable y el Técnico de Demo leen `farm_inventory_movements` (**2 filas**) en vez de `inventario_gestion_movimiento` (**12**). El equipo practicaría contra un reporte de alimento casi vacío y con otra fórmula que la de producción. |
| `captura_huevos_en_levante` | **true** | false | El formulario de levante de SanMarino pide huevos desde la semana 14; en Demo ese bloque no existe ⇒ el archivo de práctica no tendría dónde cargarlos. |
| `maneja_codigos_erp_avicola` | false | **true** | Demo muestra campos de código ERP que en SanMarino **no existen**: se practicaría con una columna de más. |
| `permite_traslado_aves_cross_etapa` | false | **true** | Demo permite un traslado que en SanMarino está prohibido: se aprendería un flujo inválido. |
| `mobile_access` | true | false | **Fuera de alcance** (acceso a la app móvil, no toca carga masiva ni costos). Se deja como está y se documenta. |

Los cuatro primeros se alinean. Es el corazón del pedido: *«antes de pasar a SanMarino no tengan
errores»* — practicar con flags distintos **fabrica** esos errores.

### 1.3 Lo que Demo ya tiene bien (no se toca)

- **Regionales idénticas** a SanMarino: las 6 opciones (`Centro`, `Costa`, `Occidente`, `Oriente`,
  `Abuelas`, `División Pollita`) en `master_lists.region_option_key`.
- **Guía genética propia**: 224 filas (`2026` × `AP`/`APN`/`C500`) en
  `guia_genetica_sanmarino_colombia`, con `guia_genetica_perfil = 'sanmarino'`.
- **Catálogo de inventario**: 62 ítems en `item_inventario_ecuador` + 61 en `catalogo_items`.
- **La función de costos ya responde**: `fn_reporte_diario_costos_postura(4,…)` devuelve **37 filas**
  hoy. El backend está bien; el problema es exclusivamente de habilitación.
- Los **5 tipos de carga masiva de Postura** están `disponible = true` en `TipoMigracion.cs`
  (Granjas, Núcleos, Galpones, Seguimiento Levante, Seguimiento Producción).

### 1.3.bis Qué va a ver realmente el equipo en el módulo (medido en el código del front)

`funciones/filtrar-tipos-visibles.funcion.ts` descarta, **en este orden**: (1) los tipos de
**ESTRUCTURA** — *«Granjas/Núcleos/Galpones, retirados de la pantalla; siguen vivos en el backend y
el historial los traduce por nombre»*; (2) los `disponible = false`; (3) los de una línea cuyo
permiso el usuario no tiene. El gate es **fail-closed y ya no pinta el tile en gris**: sin el permiso
el cargador **no se muestra**.

Consecuencias, y las dos importan:

1. **Con `carga_masiva_postura` otorgado el equipo verá exactamente 2 cargadores:**
   *Seguimiento Levante* y *Seguimiento Producción*. Es justo el ejercicio pedido — armar el archivo,
   subirlo contra una granja y validar la información.
2. 🔴 **Granjas, núcleos y galpones NO se pueden crear por carga masiva desde la pantalla.** Esto
   **confirma la decisión de conservar la estructura** en la limpieza: si se borraran las 9 granjas /
   10 núcleos / 20 galpones, el equipo se quedaría sin destino contra el cual cargar y habría que
   recrearlos a mano desde *Gestión de Granjas*. La estructura que sobrevive **es** el destino de la
   práctica.

### 1.4 Datos operativos de Demo hoy (lo que la limpieza borra)

| tabla | filas |
|---|---|
| `historico_lote_postura` | 73 |
| `seguimiento_diario_levante` | 42 |
| `lote_postura_base` | 17 |
| `inventario_gestion_movimiento` | 12 |
| `lote_registro_historico_unificado` | 12 |
| `lote_postura_levante` / `lotes` | 10 / 10 |
| `inventario_gestion_stock` | 6 |
| `espejo_huevo_produccion`, `farm_inventory_movements`, `liquidacion_cierre_lote_levante`, `lote_postura_produccion`, `seguimiento_diario_produccion` | 2 c/u |
| `farm_product_inventory` | 1 |

**Estructura que SOBREVIVE** (decisión del usuario: *«dejar solo la estructura»*): 9 granjas,
10 núcleos, 20 galpones, la guía genética, los catálogos de inventario, las listas maestras, los
3 usuarios, los 2 roles y toda la configuración de empresa.

---

## 2. Enfoque arquitectónico

Se parte en **dos entregas con riesgo muy distinto**, y no se mezclan:

### Parte A — Habilitación (segura, idempotente, por migración EF)

Sigue el patrón obligatorio de *«Features por EMPRESA»* de `CLAUDE.md` y copia el precedente
[`20260814000000_ReportesUnificadoSanmarino.cs`](../backend/src/ZooSanMarino.Infrastructure/Migrations/20260814000000_ReportesUnificadoSanmarino.cs):

- **Migración data-only**, sin cambios de schema, Designer clonado sin tocar el ModelSnapshot.
- **Localiza la empresa por `identifier`** (`'1111738751'`), nunca por `name` — el nombre es texto
  libre y una tilde de más dejaría la migración sin efecto **y sin error**.
- **Localiza los menús por `key`/`route`**, jamás por id fijo: los ids difieren local ↔ prod.
- **Idempotente**: `INSERT … WHERE NOT EXISTS` en los puentes, `IS DISTINCT FROM` en los `UPDATE`
  para no ensuciar `updated_at` de filas ya correctas.
- **`Down()` simétrico**: revierte exactamente lo que `Up()` agregó.

### Parte B — Limpieza de datos (destructiva, **NO** por migración)

⛔ **Una migración que borra datos operativos es una bomba**: se re-ejecutaría en cualquier entorno
que se levante desde cero y no hay `Down()` que la deshaga. Va como **script SQL de una sola vez**
en `backend/sql/`, con el prefijo `migracion_*` que `verificar-sql-llega-por-migracion.js` exime a
propósito («operativos de una sola vez que quedan como registro de lo que se hizo»).

- Se ejecuta **a mano**, con OK explícito del usuario, primero en local y solo después en prod.
- **Envuelto en transacción**, con un `ROLLBACK` de ensayo antes del `COMMIT` real
  (regla *«Verificar antes de limpiar datos»* de `CLAUDE.md`).
- **Filtra siempre por `company_id = (SELECT id FROM companies WHERE identifier='1111738751')`**;
  ninguna sentencia borra sin ese filtro.
- Imprime conteos **antes y después** para que quede auditoría de qué se borró.

---

## 3. Archivos a crear / modificar

**Backend — Parte A**
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260828180000_DemoListaParaPracticaCargaMasivaCostos.cs`
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260828180000_DemoListaParaPracticaCargaMasivaCostos.Designer.cs`

**Backend — Parte B**
- `backend/sql/migracion_limpieza_demo_practica_costos.sql`

**Sin cambios de código C# ni Angular.** No hace falta: todo el gating es por dato
(`company_menus` / `company_permissions` / `role_*` / flags de `companies`). Tocar código sería
introducir un `if (empresa == 'Demo')`, que es el anti-patrón que `CLAUDE.md` prohíbe explícitamente.

---

## 4. Cambios de BD, uno por uno

### Parte A (migración)

**A1 — Flags de `companies` (empresa Demo, por `identifier`):**
```sql
reportes_alimento_desde_inventario_unificado : false → true
captura_huevos_en_levante                    : false → true
maneja_codigos_erp_avicola                   : true  → false
permite_traslado_aves_cross_etapa            : true  → false
```

**A2 — `company_menus`:** habilitar en Demo, localizados por `menus.key`:
`migraciones_masivas`, `reporte_diario_costos_postura`, `reporte_tecnico_semanal`.
(El grupo padre `carga_masiva` y `reportes` **ya** están habilitados.)

**A3 — `company_permissions`:** espejo **exacto** de lo que tiene SanMarino:
```sql
carga_masiva_postura        : (no existe) → INSERT is_enabled = true
carga_masiva_pollo_engorde  : is_enabled true → false
```
`company_permissions` es **fail-closed** (`CompanyPermissionCalculos`, reglas R1/R3): sin la fila
habilitada, el permiso **no viaja en el JWT** aunque el rol lo tenga, y tampoco se ofrece en el tab
Permisos del modal de rol. Por eso A3 es requisito de A4, no un adorno.

**A4 — `role_permissions`:** otorgar `carga_masiva_postura` a `Admin Demo` y `Usuario pruebas`.

**A5 — `role_menus`:** asignar el menú `migraciones_masivas` a los dos roles de Demo.
(Ambos **ya** tienen el grupo `carga_masiva` y `reporte_diario_costos_postura`.)

**A6 — Neutralizar `carga_masiva_pollo_engorde` sin borrar nada.** Demo no tiene un solo lote de
engorde: ese permiso solo sirve para que el equipo abra tiles que no le corresponden y arme archivos
con el formato equivocado. Se apaga **en `company_permissions`** (lo de A3), **no** se borra de
`role_permissions` — es exactamente lo que hace SanMarino, y respeta la regla **R5 no destructiva**
del propio código: lo ya asignado que queda fuera se reporta como *huérfano* en la UI para que un
admin decida, no se borra en silencio. En runtime, R3 lo filtra igual.

### Parte B (script, orden por dependencias)

1. `seguimiento_diario_levante`, `seguimiento_diario_produccion`
2. `lote_registro_historico_unificado`, `historico_lote_postura`, `espejo_huevo_produccion`
3. `liquidacion_cierre_lote_levante`, `lote_etapa_levante`, `lote_huevo_items`
4. `inventario_gestion_movimiento`, `inventario_gestion_stock`,
   `farm_inventory_movements`, `farm_product_inventory`, `inventario_aves`
5. `seguimiento_reserva_alimento`, `seguimiento_reserva_aves`, `lote_galpones`
6. `lote_postura_produccion`, `lote_postura_levante`, `lote_postura_base`, `lotes`
7. `migracion_masiva` (historial de corridas previas de carga masiva)

**NO se tocan:** `farms`, `nucleos`, `galpones`, `guia_genetica_sanmarino_colombia`,
`item_inventario_ecuador`, `catalogo_items`, `master_lists`, `company_*`, `role_*`, `user_*`.

---

## 5. Reglas de negocio a respetar

- **El histórico unificado se ANULA, nunca se abandona.** `lote_registro_historico_unificado` la
  llena un trigger `AFTER INSERT`; borrar el origen **no** la limpia. Como acá se borra el lote
  entero (no se deshace un movimiento), la fila del histórico se borra explícitamente en el mismo
  paso: no queda huérfana ni contando saldo.
- **Fail-closed por empresa.** Toda sentencia lleva el filtro por `company_id` de Demo resuelto por
  `identifier`. Ninguna empresa distinta de Demo puede verse afectada — se verifica con conteos
  antes/después de las 5 empresas.
- **Refactor ≠ cambio de comportamiento.** No se toca una línea de C# ni de Angular: el
  comportamiento de SanMarino, Ecuador, Panamá y Santa Reyes queda **byte a byte idéntico**.

---

## 6. Casos de prueba / validación

**Parte A**
1. `dotnet build` → 0 errores, 0 advertencias nuevas.
2. `dotnet test` → suite completa en verde (3487 tests hoy).
3. Aplicar la migración en local y **re-ejecutar `fn_menu_usuario`** con `admin.demo`:
   - `Carga Masiva` deja de estar vacío y muestra **Migración Manual**.
   - `Reportes` pasa de 2 a 4 hijos, con **Reporte Diario Costos Postura** e **Informe RA Pesadas**.
4. **Idempotencia:** correr la migración dos veces ⇒ los conteos no cambian en la segunda.
5. **No-regresión multiempresa:** conteos de `company_menus` / `company_permissions` de las empresas
   1, 3, 5 y 6 **idénticos** antes y después.
6. `fn_reporte_diario_costos_postura(4,…)` sigue respondiendo.

**Parte B**
7. Ensayo en **transacción con `ROLLBACK`**: la limpieza corre sin error de FK y los conteos vuelven
   solos al estado previo.
8. Tras el `COMMIT` real: los datos operativos de Demo en **0**, y granjas 9 / núcleos 10 /
   galpones 20 **intactos**.
9. Conteos de las otras 4 empresas **sin una sola fila de diferencia**.
10. Smoke funcional: entrar como `admin.demo`, abrir Migración Manual, descargar la plantilla de
    **Granjas** y la de **Seguimiento Levante**, y verificar que el reporte de costos abre sin error
    (vacío es el resultado correcto tras la limpieza).

---

## 7. Riesgos y cómo se acotan

| Riesgo | Mitigación |
|---|---|
| La limpieza toca otra empresa | Filtro por `company_id` resuelto por `identifier` en **cada** sentencia + conteos de control de las 5 empresas. |
| La migración corre en prod antes de tiempo | Decisión del usuario: **no se despliega**. Queda commiteada; el deploy es una acción aparte con OK explícito. |
| Encender `reportes_alimento_desde_inventario_unificado` mueve números conciliados | En Demo **no hay nada conciliado** — es el entorno de práctica y además se limpia. El riesgo que documenta la memoria aplica a SanMarino, no acá. |
| Quitar `maneja_codigos_erp_avicola` rompe datos con código ERP | Los datos operativos se borran en la Parte B; no queda ninguna fila que dependa del flag. |
| Otra sesión de Claude Code está tocando el tracker | Mi bloque se **agrega al final**, separado por `---`. No se borra ni se edita nada de los bloques abiertos (DUP-DIA y hora-de-llegada). |

---

## 8. Runbook de la práctica (lo que el equipo de costos va a hacer en Demo)

Se desprende de cómo está construido el módulo, no de una suposición: los cargadores de estructura
están retirados de la pantalla y los dos de seguimiento declaran `requiereLote = true` en
`TipoMigracion.cs`.

1. **La granja ya está.** Las 9 granjas / 10 núcleos / 20 galpones sobreviven a la limpieza: son el
   destino contra el cual se carga. No se crean por carga masiva.
2. **El lote se crea a mano**, en *Lote → Lote Postura* (`/config/lote-management`), que Demo ya
   tiene habilitado. La carga masiva **no** crea lotes: los dos cargadores exigen un lote existente.
   ⚠️ La guía genética de Demo sólo tiene el año **2026** (razas `AP`, `APN`, `C500`): un lote creado
   con otro año no tendrá contra qué compararse en los reportes.
3. **Se arma y se sube el archivo** en *Carga Masiva → Migración Manual*, eligiendo
   **Seguimiento Levante** o **Seguimiento Producción**. La pantalla ofrece la plantilla, valida en
   seco (*dry run*) y reporta errores por fila/columna antes de escribir nada.
4. **Se valida el resultado** en *Reportes*: **Reporte Diario Costos Postura**, **Reporte Contable**,
   **Reporte Técnico Sanmarino** e **Informe RA Pesadas** — los cuatro leyendo ya la misma fuente de
   alimento que producción, gracias al flag alineado.

---

## 9. Fuera de alcance (explícito)

- **`mobile_access`** de Demo: queda en `false`. Es acceso a la app móvil, no toca carga masiva ni
  costos; encenderlo es decisión aparte.
- **Los otros 22 menús** que SanMarino tiene y Demo no (ItalJira, Mapas, Vacunación, Implementación,
  Empresas, Geografía, db_studio, Lote Reproductora Postura, Integración Panamá…): quedan apagados
  por la decisión de alcance. Si mañana se quiere el espejo completo, es agregar filas a
  `company_menus` con el mismo patrón.
- **El deploy a producción.** La migración queda escrita, probada y commiteada; aplicarla es una
  entrega aparte.
