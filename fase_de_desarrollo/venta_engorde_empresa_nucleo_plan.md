# Venta de pollo engorde (Panamá y Ecuador): campo Núcleo + «Empresa de venta» parametrizable

Fecha: 19-sep-2026 · Alcance: front + backend + migración data-only (sin DDL).

## Pedido (textual, resumido)

1. En el módulo **Venta de Pollo Engorde**, para **Panamá y Ecuador**, agregar el campo **Núcleo**.
2. En el **modal de venta**, un campo nuevo **Empresa**: a qué empresa se vendió / se envió. Es **parametrizable
   desde Listas Maestras** y **dinámico por empresa**; la migración lo crea con un valor por defecto: **Planta**.
3. Quedar guardado en la venta.
4. En la **tabla inicial**, poder **filtrar por la empresa de venta**: al filtrar por «Planta» se ve lo enviado a esa
   empresa desde la granja / núcleo / galpón / lote que esté seleccionado.

## Estado actual (medido en el código y en la copia de producción local)

- La pantalla (`movimientos-pollo-engorde-list`) tiene la cascada **Granja → Galpón → Lote**. La lógica de núcleo ya
  existe en el `.ts` (`selectedNucleoId`, `nucleos`, `onNucleoChange`, `nucleoOrigenId` en la búsqueda) pero **el
  template no dibuja el select**, y `onNucleoChange` **nunca recarga los movimientos** (bug latente: dejaría la tabla
  vacía). Los dos modales de venta (`modal-movimiento-pollo-engorde` = Ecuador/venta por granja y
  `modal-venta-panama`) agrupan por galpón sin mostrar el núcleo.
- `movimiento_pollo_engorde.planta_destino` (`varchar(200)`, entidad `PlantaDestino`) **ya existe** y **ya lo escribe
  la carga masiva de ventas** (columna «Planta Destino» del Excel → `fn_migracion_venta_engorde`). Ninguna pantalla
  lo usa y **está en NULL en todas las filas** de la copia local (1.627 ventas Ecuador + 297 Panamá).
- Solo **ItalcolEcuador (id 3)** e **ItalcolPanama (id 5)** tienen movimientos de engorde.
- `MasterListService.UpdateAsync` **borra y recrea todas las opciones** al guardar una lista ⇒ **los ids de las
  opciones cambian en cada edición**. Guardar el id de la opción en la venta se rompería la primera vez que alguien
  edite la lista.

## Decisiones (y por qué)

| # | Decisión | Por qué |
|---|---|---|
| D1 | **Se reutiliza `planta_destino`** como la «Empresa de venta». Nombre de cable: `plantaDestino` (ya existe en los DTO de crear/actualizar). | Cero DDL y **cero cambios al `ModelSnapshot`** (la otra sesión edita migraciones/snapshot en paralelo); un solo campo «destino» en vez de dos (la carga masiva ya lo llena, así que el filtro sirve para las ventas cargadas por Excel y por pantalla). |
| D2 | Se guarda el **texto** de la opción, no su id. | `MasterListService.UpdateAsync` regenera los ids; el texto es la identidad estable (mismo criterio de `traslado_de_huevos_planta_destino`). Renombrar una opción no toca el histórico; el filtro además ofrece los valores que ya existan en las filas. |
| D3 | Lista maestra `venta_pollo_engorde_empresa` («Empresa de venta (pollo engorde)»), **una por (empresa, país)**, sembrada por **migración data-only idempotente** con la opción **Planta**. | Es lo pedido; mismo patrón que `20260824120000_SeedListasMaestrasTrasladoSantaReyes`. Se siembra a **todas** las empresas con país (5 hoy) para que cualquiera la administre sin ayuda de desarrollo; solo Ecuador y Panamá tienen ventas de engorde. |
| D4 | **Sin flag nuevo en `companies`**: la señal es que la lista tenga opciones para la empresa/país activos (**data-driven, fail-closed**). Sin lista o sin opciones ⇒ el campo, el filtro y la columna **no se dibujan** y la UI queda idéntica a la de hoy. | La parametrización pedida ES la lista; no hay `if (país == X)` ni `if (empresa == 'X')`. |
| D5 | Con lista disponible, **Empresa es obligatoria al CREAR una venta** y viene **preseleccionada** con la primera opción (Planta). Al **editar** es opcional (ventas viejas sin empresa siguen editables). | «Por defecto Planta» sin fricción, pero sin dejar la venta sin destino por descuido. Si la lista falla o está vacía, no bloquea. |
| D6 | El backend **no exige** que el valor pertenezca a la lista; solo **normaliza** (trim; vacío ⇒ null) y valida el largo (≤ 200). | La lista es editable y la carga masiva escribe texto libre; validar pertenencia rompería ventas históricas y la edición. |
| D7 | Filtro **en el cliente** (igual que Tipo/Estado): la pantalla ya trae hasta 3.000 movimientos de la granja/núcleo/galpón/lote elegidos. | Consistente con el resto de filtros de la tabla; no hay parámetro de API sin uso. |
| D8 | «Núcleo»: se dibuja el select en la cascada (**Granja → Núcleo → Galpón → Lote**), se corrige `onNucleoChange`, y el núcleo/galpón se muestran en la tabla, el Excel y los dos modales. | Pedido 1. Los ids `nucleoOrigenId`/`galponOrigenId` se agregan al DTO de lectura (con respaldo al núcleo/galpón del lote para filas viejas). |

## Cambios

### Base de datos
- Migración **data-only** `20260919120000_SeedListaMaestraEmpresaVentaEngorde` (+ `.Designer.cs` clonado del
  snapshot, **sin tocar el snapshot**): por cada `company_pais` inserta la lista `venta_pollo_engorde_empresa` y la
  opción `Planta` (orden 0) con `WHERE NOT EXISTS` (clave natural `key+company_id+country_id` y
  `master_list_id+value`). `Down()`: borra las listas de esa key (las opciones caen por el `ON DELETE CASCADE`).
- `backend/sql/verificar_empresa_venta_engorde.sql` (solo lectura, prefijo exento del gate): listas sembradas,
  opciones y conteo de ventas por empresa. Sirve como verificación post-deploy.

### Backend
- `MovimientoPolloEngordeCalculos.NormalizarEmpresaVenta(string?)` (pura): trim, vacío ⇒ null, > 200 ⇒
  `InvalidOperationException`.
- DTO lectura `MovimientoPolloEngordeDto`: + `PlantaDestino`, `NucleoOrigenId`, `GalponOrigenId` (al final, con
  default null ⇒ compatible). `ToDto` los llena (núcleo/galpón con respaldo al lote de origen).
- `CreateVentaGranjaDespachoDto` y `CreateVentaPanamaDespachoDto`: + `PlantaDestino`.
- Servicios: `CreateVentaGranjaDespachoAsync` (Ecuador/genérico), `CreateVentaPanamaDespachoAsync` (Panamá),
  `CreateAsync` y `UpdateAsync` normalizan y guardan `PlantaDestino`.
- Tests xUnit: `NormalizarEmpresaVenta` (null, blanco, trim, 200 ok, 201 falla).

### Frontend (`features/movimientos-pollo-engorde/`)
- `services/empresa-venta-engorde.service.ts`: lee la lista maestra por key (`MasterListService.getByKey`), devuelve
  los textos limpios y sin duplicados; ante cualquier error, `[]` (fail-closed).
- `funciones/empresa-venta.funcion.ts` (pura, con spec): `unirOpcionesEmpresaVenta` (lista ∪ valor guardado ∪ valores
  vistos, sin repetir por mayúsculas), `coincideEmpresaVenta` (filtro, incluye «sin empresa» solo para ventas),
  `resumenEmpresaVenta` (una empresa / «Varias» para despachos multi-lote).
- Modelos/DTO: `plantaDestino` en las interfaces de crear venta (Ecuador y Panamá) y lectura; `nucleoOrigenId` /
  `galponOrigenId` en lectura. `mapear-*-dto.funcion.ts` envían `plantaDestino`.
- **Modal Ecuador** (`modal-movimiento-pollo-engorde`) y **modal Panamá** (`modal-venta-panama`): select
  «Empresa de venta» (default primera opción, obligatorio al crear si hay lista), núcleo en los títulos de galpón.
  Detalle de solo lectura muestra la empresa.
- **Lista** (`movimientos-pollo-engorde-list`): paso «Núcleo» en la cascada; `onNucleoChange` recarga; filtro
  «Empresa de venta» en la tarjeta Filtros (+ «Sin empresa»); la empresa se muestra en la columna Destino de las
  ventas (y «Varias» en el despacho agrupado); núcleo/galpón en Origen y en el sub-detalle; Excel con
  «Núcleo origen», «Galpón origen» y «Empresa de venta».

## Reglas de negocio

- Venta nueva con lista disponible ⇒ empresa obligatoria (default = primera opción). Sin lista ⇒ comportamiento de hoy.
- La empresa se guarda por línea (igual que placa/conductor); un despacho multi-lote crea todas las líneas con la
  misma empresa.
- Filtro «Empresa»: coincidencia exacta sin distinguir mayúsculas ni espacios; «Sin empresa» = ventas sin valor.
- Nada cambia en el saldo de aves, pesos, estados ni en la liquidación.

## Casos de prueba

Backend (xUnit): normalización (null → null; «   » → null; « Planta » → «Planta»; 200 caracteres → ok; 201 →
excepción con mensaje). Migración: dos pasadas dentro de `BEGIN…ROLLBACK` sobre la copia local ⇒ 1.ª pasada = una lista
+ una opción por (empresa, país), 2.ª pasada = 0; el resto de `master_lists` intacto.

Front (Karma): unión de opciones (lista + guardado + vistos, sin duplicados por mayúsculas, ignora vacíos);
`coincideEmpresaVenta` (todas, exacta, sin empresa solo ventas, mayúsculas); resumen de despacho (misma empresa /
Varias / ninguna).

Smoke (backend aislado sobre un clon de la copia local, nunca sobre la BD compartida ni RDS):
1. `POST venta-despacho` (Panamá) y `POST venta-despacho` (genérico/Ecuador) con `plantaDestino: "Planta"` ⇒ 201 y el
   campo vuelve en el DTO; sin `plantaDestino` ⇒ null; 201 caracteres ⇒ 400.
2. `PUT` de un movimiento Pendiente cambiando la empresa.
3. UI: la cascada muestra Núcleo y recarga; el modal ofrece «Empresa de venta» con Planta preseleccionada; guardar
   muestra la empresa en la tabla; el filtro por «Planta» deja solo esas ventas; el Excel trae las columnas nuevas.
4. Empresa **sin** lista (Colombia/Demo): la pantalla y los modales quedan **idénticos** a hoy.

## Riesgos y mitigaciones

- **Empresa nueva sin lista**: el campo no aparece hasta que exista la lista (crearla desde Config → Listas maestras
  con la key `venta_pollo_engorde_empresa`, o con una migración seed como esta). Queda documentado.
- **Texto libre histórico** en `planta_destino` (carga masiva): sigue visible y filtrable (el filtro suma los valores
  presentes en las filas).
- **Build de Infrastructure** tarda decenas de minutos (754 Designers): se lanza una sola vez, en segundo plano.
- Migración data-only: no hay DDL, no toca el snapshot ni el historial fuera de su propia fila.
