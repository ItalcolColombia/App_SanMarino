# Carga masiva de Levante y Producción para Santa Reyes — plan

**Fecha:** 4-sep-2026
**Pedido:** validar todo lo que es Santa Reyes para poder hacer la carga masiva de **Levante** y
**Producción**, dejar la **plantilla de descarga parametrizada para esa empresa** y que traiga un
**ejemplo lleno** que guíe a las personas.

Relacionado: [`santa_reyes_implementacion_plan.md`](santa_reyes_implementacion_plan.md),
[`santa_reyes_silos_bodegas_inventario_plan.md`](santa_reyes_silos_bodegas_inventario_plan.md),
entrega del 1-sep-2026 (`Desktop/Manual_Carga_Masiva_Postura/`).

---

## 0. Auditoría previa (hecha) — de dónde sale este plan

Auditoría de 10 dimensiones con verificación adversarial y **consultas reales contra la BD local**
(`sanmarinoapplocal` en `127.0.0.1:5433`). 26 hallazgos confirmados, 3 refutados.

**Flags de Santa Reyes medidos en `companies` (id 6):**

| flag | valor |
|---|---|
| `clasificacion_huevo_por_items` | **true** |
| `oculta_machos_en_postura` | **true** |
| `consumo_alimento_solo_hembras` | **true** |
| `maneja_inventario_por_silo` | **true** |
| `maneja_codigos_erp_avicola` | **true** |
| `permite_traslado_aves_cross_etapa` | **true** |
| `semanas_ciclo_postura_por_raza` | **true** |
| `huevo_primera_postura_hasta_semana` | 22 |
| `captura_huevos_en_levante` | **false** |
| `maneja_alimento_por_galpon` | **false** (stock a nivel granja) |
| `requiere_validacion_seguimiento_diario` | **false** |

**Datos:** 1 granja (109), 1 lote (152 «LOTE 218A»), LPL 44 **Cerrado + liquidado**, LPP 20 vivo
⇒ el lote **ya es elegible para las dos migraciones**. 39 silos, 45 ítems de inventario,
0 filas en `lote_huevo_items`, 2 usuarios (roles 30 «Santa Reyes Administrador» y 31 «Santa Reyes
Implementador»).

### Lo que YA está resuelto (no se toca)

- La hoja **`Huevos`** por ítem del catálogo existe y está gateada por `clasificacion_huevo_por_items`
  (`MigracionService.Historicos.cs:122`), con **paridad aritmética exacta** con el alta manual
  (`huevo_tot` = suma de ítems, `huevo_inc` = 0, las 11 columnas en 0) y el **mismo shape** de
  `metadata.huevoItems` que escribe `ProduccionService`.
- La **lista blanca de tipos de huevo del lote (F7.3)** se aplica en la carga masiva: no es la puerta
  de atrás de la restricción (`MigracionService.HuevosPostura.cs:72-94`).
- La **ventana de fecha NO corta** la carga masiva, y eso es correcto y está documentado
  (`VentanaFechaRegistroCalculos.cs:35-39`): un histórico escribe fechas viejas a propósito. La única
  guarda de fecha es la correcta (anterior al encaset = Error, futura = Advertencia).
- El **parseo tolera un archivo sin columnas de machos**: la única columna `Requerida:true` de levante
  y producción es `Fecha` (test `Postura_SoloFechaEsRequerida`), una celda ausente se lee como vacía
  (`EnteroNoNeg`→0, `DecimalNoNeg`→null) y las fns SQL insertan igual (verificado en `BEGIN…ROLLBACK`
  contra el lote 152).
- Los **permisos ya están dados**: `company_permissions` de la empresa 6 y `role_permissions` de los
  roles 30/31 tienen `carga_masiva_postura`.
- El **mecanismo para parametrizar la plantilla ya existe y está en producción**:
  `PonerEncabezadosSin` (`MigracionService.Plantillas.cs:55`) y el precedente de esquema alterno por
  flag `SeguimientoPolloEngordeMixto` (`MigracionService.SeguimientoEngorde.cs:680`). **Quitar
  columnas opcionales no rompe archivos viejos** (`LeerDatosConEsquema` solo corta por requeridas).
- La **guía genética propia** de Santa Reyes no participa del camino de carga masiva, y está bien así.

---

## 1. Enfoque arquitectónico

Tres reglas rectoras, todas del patrón «features por empresa» de CLAUDE.md:

1. **La decisión de qué columnas emitir es lógica PURA**, en
   `Application/Calculos/PlantillaPosturaCalculos.cs`, con tests xUnit. El generador solo resuelve
   los flags y delega. Prohibido `if (empresa == 'Santa Reyes')`.
2. **La plantilla cambia; el ESQUEMA DE VALIDACIÓN no.** Se emite con `PonerEncabezadosSin`, así que
   las columnas omitidas siguen siendo opcionales al importar y **un archivo viejo sigue siendo
   válido** (delta cero para Sanmarino / Demo / Ecuador / Panamá, por construcción).
3. **Flags por la empresa DUEÑA DE LA GRANJA, fail-closed** — se extiende la proyección que ya hace
   `ResolverLotePosturaCtxAsync`, no se inventa otra fuente.

---

## 2. Fases

### F1 · Habilitar el módulo para Santa Reyes  🔴 BLOQUEANTE

**Hallazgo:** `20260807230000_RestringirMigracionesMasivasASanmarino` dejó el módulo **solo para
Agroavicola Sanmarino** (decisión de negocio explícita de ago-2026, no un descuido). Medido con
`fn_menu_usuario` sobre los 2 usuarios reales de la empresa 6: `ve_migraciones_masivas = false`.
Probado en transacción revertida: **faltan exactamente 3 filas** — 1 en `company_menus` y 2 en
`role_menus`. El grupo padre `carga_masiva` **no necesita fila propia** (`fn_menu_usuario` sube los
ancestros sola, decisión D3) y `menu_permissions` está vacío para ese menú.

- [ ] F1.1 Migración EF data-only idempotente `HabilitarMigracionesMasivasSantaReyes`
      (timestamp posterior a `20260828180000`), con su **`.Designer.cs`** — sin Designer, EF no la ve
      y nunca se aplica.
- [ ] F1.2 Localizar por `menus.key = 'migraciones_masivas'` y `companies.name = 'Santa Reyes'`,
      nunca por id. `INSERT … WHERE NOT EXISTS` + `UPDATE … WHERE is_enabled IS DISTINCT FROM true`
      (una fila puede existir apagada).
- [ ] F1.3 Apagar `carga_masiva_pollo_engorde` en `company_permissions` de Santa Reyes: la empresa no
      tiene lotes de engorde y con el permiso prendido vería 4 tiles que no le aplican. Se apaga la
      fila de empresa, **no** se borran las de `role_permissions`.
- [ ] F1.4 Verificar con `fn_menu_usuario` para los 2 usuarios: antes `f/f`, después `t/t`; y que
      ningún usuario de otra empresa gane un menú.

### F2 · Plantilla parametrizada por empresa  🔴 el pedido central

Hoy `GenerarPlantillaSeguimientoAsync` emite las **43 columnas** del esquema estático para toda
empresa. Para Santa Reyes **28 no aplican** y el formulario vivo ya las oculta con los mismos flags
(`modal-seguimiento-diario.component.html`: `@if (!ocultaMachosEnPostura)`,
`@if (!clasificacionHuevoPorItems)`).

- [ ] F2.1 `Application/Calculos/PlantillaPosturaCalculos.cs` (puro, sin EF):
      `ColumnasOcultas(esLevante, flags)` devuelve el conjunto de **títulos** a omitir.
      - `OcultaMachosEnPostura` ⇒ `Mort M`, `Sel M`, `Error Sexaje M`, `Consumo M (kg)`, `Peso M (g)`,
        `Uniformidad M`, `Coef. Variación M`.
      - `ConsumoAlimentoSoloHembras` ⇒ `Alimento 1 M`, `Consumo Alimento 1 M`, `Alimento 2 M`,
        `Consumo Alimento 2 M`.
      - `ClasificacionHuevoPorItems` (solo producción) ⇒ `Huevo Total`, `Huevo Incubable`,
        `Peso Huevo (g)` y las **11 categorías** — espejo exacto del modal vivo.
      - `!CapturaHuevosEnLevante` (solo levante) ⇒ las 11 categorías + `Peso Huevo (g)`.
      - Con **todos los flags apagados el conjunto es VACÍO** ⇒ delta cero para las demás empresas.
- [ ] F2.2 Tests xUnit `PlantillaPosturaCalculosTests`: flags apagados ⇒ 0 columnas ocultas y la
      plantilla emite las 43 de siempre; Santa Reyes ⇒ la lista exacta; ninguna columna oculta puede
      ser `Requerida:true` (test que recorre el esquema y lo prueba, así una columna requerida futura
      no se puede ocultar por accidente).
- [ ] F2.3 `ResolverLotePosturaCtxAsync`: agregar `OcultaMachosEnPostura`,
      `ConsumoAlimentoSoloHembras` y `ManejaInventarioPorSilo` a la proyección (misma consulta, mismo
      fail-closed `?? false`).
- [ ] F2.4 Generador: `PonerEncabezadosSin(ws, esquema, ocultas)`.
      ⚠️ **Trampa medida:** hoy las letras de los dropdowns se calculan con `IndiceColumna` sobre el
      esquema COMPLETO (`Historicos.cs:196-199`); al omitir columnas las letras se corren y los
      desplegables caen en la columna equivocada. Hay que calcular el índice sobre las **columnas
      emitidas** (que es justo lo que `PonerEncabezadosSin` devuelve).
- [ ] F2.5 Instrucciones: ramificar el texto por empresa (patrón `SeguimientoEngorde.cs:764-789`) y
      sumar el **orden operativo** «cargar levante → cerrar/liquidar → trasladar → cargar producción»
      con su motivo (el consumo descuenta de la ubicación **actual** del lote, no la histórica).

### F3 · Hoja «Ejemplo» llena  🔴 el pedido central

Ninguna de las 9 plantillas del módulo tiene hoja de ejemplo: `HojaInstrucciones` solo escribe texto.
El importador **ignora toda hoja que no sea** `Datos` / `Alimento` / `Movimientos Aves` /
`Movimientos Huevos` / `Huevos`, así que una hoja `Ejemplo` es segura.

- [ ] F3.1 `Application/Calculos/MigracionEjemploPosturaCalculos.cs` (puro): filas de ejemplo
      **parametrizadas por los mismos flags** — no puede enseñar machos ni las 11 categorías a una
      empresa que las tiene apagadas.
- [ ] F3.2 Tests xUnit: toda columna citada por el ejemplo **existe en el esquema y no está oculta**
      (test que cruza ejemplo × esquema × columnas ocultas: es lo que impide que el ejemplo enseñe
      una columna que la plantilla no emite).
- [ ] F3.3 Helper `HojaEjemplo` en `MigracionService.Plantillas.cs`, al lado de `HojaInstrucciones`:
      encabezados reales de la hoja `Datos` + 3 filas llenas y coherentes, y bloques aparte para
      `Alimento`, `Movimientos Aves` y (si aplica) `Huevos`. Alimento e ítem de huevo salen de los
      catálogos **reales de la empresa** que ya se cargan para `Referencias`.
- [ ] F3.4 Rótulo visible «ESTA HOJA NO SE IMPORTA — es solo guía» y `Instrucciones` movida al frente
      (`MoveToStart`) para que el archivo abra por donde se explica.

### F4 · Hoja «Huevos» y «Movimientos Huevos» de Santa Reyes  🔴 BLOQUEANTE

- [ ] F4.1 La plantilla ofrece los **28 ítems de la EMPRESA** y el parseo solo acepta los declarados
      en `lote_huevo_items` (hoy **0 filas**) ⇒ el usuario llena la hoja del desplegable y el archivo
      se rechaza entero. Fix: armar `Referencias` y el dropdown con la **misma consulta que el
      parseo** (los ítems del LOTE). Si el lote no declaró ninguno, **no emitir la hoja** y decirlo en
      `Instrucciones` («declaralo al editar el lote»), en vez de emitir una hoja inusable.
- [ ] F4.2 `Movimientos Huevos` solo acepta las **11 categorías fijas**; con clasificación por ítems
      esas columnas quedan en 0, así que la validación de disponibilidad **rechaza el archivo
      entero**. Fix de alcance acotado: **no emitir la hoja** para empresas con
      `clasificacion_huevo_por_items` y explicar en `Instrucciones` que los traslados/ventas de huevo
      se cargan por pantalla. (Hacerla por ítem es backend nuevo: se registra como pendiente, no se
      improvisa acá.)
- [ ] F4.3 `Huevo Total` de la hoja `Datos` se **descarta en silencio** cuando el día trae ítems
      (su gemela `Huevo Incubable` sí emite Error). Fix: advertencia explícita «manda el desglose»,
      con su caso en `MigracionPosturaCalculosTests`.

### F5 · Silo real en la carga masiva, espejando el seguimiento diario  🔴 BLOQUEANTE

Santa Reyes ubica el alimento **por silo** (Fases B/C/D, en producción). El módulo de migraciones no
mencionaba silos ni una vez: la hoja `Alimento` reventaba fila por fila y el consumo del seguimiento
se mandaba con `SiloId = null`, así que **el día se guardaba y el inventario quedaba intacto**, con la
simulación del dry-run dando luz verde porque sumaba todos los silos en una posición.

**El patrón que se espeja** (mapeado con file:line contra el alta manual, 25 agentes):
`InventarioUbicacionSiloCalculos.ResolverModo/ValidarUbicacion/NormalizarUbicacion` +
`ConsumoSiloCalculos.ValidarClaves` sobre `lote_silos` + `ItemConsumoKey.SiloId` +
`metadata.siloId` solo cuando existe.

- [x] F5.1 **Columnas nuevas en el esquema** (fuente única de lectura): `Silo` y `Silo Origen` en la
      hoja `Alimento`; `Silo Alimento 1/2 H-M` en la hoja `Datos` — el silo va **por ítem**, no por
      fila, igual que el formulario diario (dos alimentos del mismo día pueden salir de silos
      distintos y el backend los descuenta por separado).
- [x] F5.2 **Emisión por flag**: solo las empresas con `maneja_inventario_por_silo` ven esas columnas.
      En modo clásico el servicio de inventario **rechaza** un movimiento que traiga silo
      (`MensajeSiloNoAplica`), así que ofrecerlas rompería el archivo. Sanmarino sigue con sus 43.
- [x] F5.3 **Resolución por nombre, fail-closed**: el silo se busca entre los **activos de la granja de
      esa misma ubicación** (`fs.Activo && fs.DeletedAt == null`, el criterio del BACKEND y no el del
      selector, que omite `fs.Activo`). En la hoja `Datos` se valida además contra `lote_silos` con el
      mensaje del alta manual (`MensajeSiloNoAsignadoAlLote`), **antes de insertar nada**.
- [x] F5.4 **Propagación**: `SiloId` en el ingreso, `FromSiloId`/`ToSiloId` en el traslado, `SiloId` en
      el consumo; y `ItemSeguimientoDto.SiloId` → `ItemConsumoKey(itemId, true, siloId)` →
      `AplicarConsumoAsync`. Con silo, el consumo va por `RegistrarConsumoNivelGranjaAsync` (el stock
      vive a nivel granja con núcleo/galpón en NULL, decisión de la Fase B).
- [x] F5.5 **La clave del consumo se acumula CON el silo** (`Dictionary<ItemConsumoKey, decimal>`).
      Aplanar antes a `Dictionary<int, decimal>` era irreversible: dos filas del mismo alimento en
      silos distintos llegaban sumadas y se descontaban todas del primero.
- [x] F5.6 **`PosicionAlimento` con silo**: la simulación del balance y el stock que la alimenta
      discriminan por silo, así que el dry-run mide el silo real. Con silo `null` la posición es
      idéntica a la de hoy.
- [x] F5.7 **Idempotencia intacta**: el segmento del silo se agrega a `ClaveIdempotencia` **solo cuando
      hay silo**, así que la clave de toda fila ya cargada por una empresa sin silo rinde el mismo
      string y un reimport no vuelve a aplicar nada.
- [x] F5.8 **`metadata.siloId`**: `SerializarItem` pasa a ser espejo exacto de `ItemAMetadata` (la clave
      solo cuando es > 0). Sin esto, editar un día migrado devolvería el alimento a «sin silo».
- [i] `ReplicarPorSilo` no hace falta duplicarlo: la carga masiva reusa `AplicarConsumoAsync`, que ya
      lo aplica dentro de `ResolverItemsBAsync`. Cualquier código nuevo que reconstruya ese mapeo
      tiene que repetirlo, o todo consumo con silo muere con «el ítem no existe».
- [i] `RegistrarConsumoNivelGranjaAsync` es **fail-open** con el silo (no valida granja ni actividad).
      Los tres agujeros (silo de otra granja, silo inactivo, silo con el flag apagado) los cierra el
      PARSEO, que resuelve el nombre solo entre los silos activos de esa granja.

### F6 · Validación

- [ ] F6.1 `dotnet build` 0 errores / 0 advertencias nuevas + `dotnet test` verde.
- [ ] F6.2 `yarn build` (solo si se toca front).
- [ ] F6.3 **Smoke doble, obligatorio** (regla de features por empresa):
      - **Sanmarino (flags OFF)**: la plantilla de Levante y la de Producción salen **byte a byte
        iguales** a las de hoy (mismas hojas, mismas 43 columnas, mismo orden) salvo la hoja `Ejemplo`
        nueva. Delta cero.
      - **Santa Reyes (flags ON)**: descargar las dos plantillas del lote 152, verificar las columnas
        emitidas con `openpyxl`, llenar el ejemplo y correr `/validar` + `/importar` contra el backend
        local.
- [ ] F6.4 Apagar el backend del smoke y dejar el puerto libre.

---

## 3. Reglas de negocio y casos de prueba

| Caso | Esperado |
|---|---|
| Plantilla Levante, empresa sin flags | 43 columnas, idénticas a hoy |
| Plantilla Levante, Santa Reyes | sin las 7 de machos, sin las 4 de alimento M, sin las 11 categorías ni `Peso Huevo (g)` |
| Plantilla Producción, Santa Reyes | ídem + sin `Huevo Total` / `Huevo Incubable` / `Peso Huevo (g)`; con hoja `Huevos`; sin `Movimientos Huevos` |
| Archivo viejo (43 columnas) subido por Santa Reyes | sigue siendo válido: las columnas de más son Advertencia, no Error |
| Archivo Santa Reyes sin columnas de machos | mortalidad/sel/error de machos en 0, `cons_kg_m` en 0/NULL, `aves_h_actual` recalculada |
| Hoja `Huevos` con ítem no declarado en el lote | Error con el mensaje de `HuevoItemsCalculos`, y **la plantilla ya no lo ofrecía** |
| Plantilla de una empresa con silo | sin hoja `Alimento` y sin las columnas `Alimento 1/2 H-M` |
| Archivo con hoja `Alimento`, empresa con silo | rechazado con mensaje accionable, sin insertar nada |
| Plantilla de una empresa sin silo | hoja `Alimento` y columnas de inventario intactas |

---

## 4. Fuera de alcance (registrado, no se improvisa)

- `MigracionController` **no tiene un solo `[Authorize]` por permiso** y la ruta del front no lleva
  `permissionGuard`: el gate del módulo es 100 % de UI. No es fuga entre empresas (la empresa efectiva
  la valida `ActiveCompanyMiddleware`), pero la «restricción a Sanmarino» de ago-2026 nunca fue real.
  **Deuda conocida, no se arregla de paso en esta migración.**
- `Movimientos Huevos` por ítem del catálogo (backend nuevo por reporte).
- `AplicarMovimientosHuevosAsync` no escribe `TotalHuevos` (bug preexistente, ya registrado).
- Reacción al cambio de empresa activa en la pantalla del módulo, y el mensaje de error que se pierde
  por llegar como `Blob`.
