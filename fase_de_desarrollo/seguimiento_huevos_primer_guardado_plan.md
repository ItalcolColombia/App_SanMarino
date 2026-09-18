# Seguimiento diario (Producción y Levante): los huevos «quedan en 0» y hay que registrar 2-3 veces

Fecha: 18-sep-2026 · Solo frontend · Sin migración, sin flag, sin backend.

## Reporte

Operación de Santa Reyes: «cuando registramos la PDN y le damos guardar, guarda el movimiento pero queda en
0, y toca hacerlo dos o hasta 3 veces para que registre el ingreso». La captura que adjuntan (P-LOTE 217A, La
Esperanza, 14/09) muestra el rastro: un primer registro del día con mortalidad 15 y consumo 1.215 kg **y huevos
0**, y otros dos registros del mismo día, **solo huevos**, idénticos (7.010 = Primera 6.600 + PNC 410).

## Qué se descartó primero (medido, no supuesto)

- **Backend.** Con el request correcto guarda bien: alta desde el navegador → `201`, `huevo_tot = 7010` y
  `metadata.huevoItems` con los dos ítems (registro #677 del clon). `HuevoItemsLoteValidacion` *lanza* ante un ítem
  inválido, no descarta en silencio; el espejo de huevos solo escribe su propia tabla. El backend no pierde huevos.
- **Cola offline (PWA).** El reenvío conserva `huevoItems` (`SyncPushService.Produccion` usa `request with {…}`).
- Por lo tanto los huevos **no llegan en el request** del primer guardado, o el usuario no llegó a verlos. Todo
  el problema está en el modal de Producción y en cómo el padre lo alimenta.

## Causas (todas reproducidas en el navegador: clon de la BD local, empresa 6 = Santa Reyes, back aislado)

| # | Defecto | Cómo se ve | Medido |
|---|---|---|---|
| D1 | `ModalSeguimientoDiarioComponent.ngOnChanges()` llama `resetForm()` ante **cualquier** cambio de `@Input` con el modal abierto (el de Levante ya tiene la guarda; este no). | El formulario se vacía solo. La causa típica en producción: `informacion-lote` (consulta pesada, sobre todo en lotes con años de historia) llega **después** de abrir el modal y cambia `[fechaEncaset]` de `null` a la fecha → se pierden los huevos ya tecleados y el operario, parado en otra pestaña, no lo nota. También ocurre al cambiar `loteId`, y **cada vez que `loading` cambia** (al guardar, y otra vez al terminar). | Huevos 656:5000 + 667:300 tecleados a los 2,6 s → a los 5,1 s (llegada de `informacion-lote`) quedan `[]`, total 0. |
| D2 | Guardado con error (400/500/red): el modal sigue abierto pero **vacío** (mismo `resetForm()` por el cambio de `loading`). | Hay que teclear todo de nuevo. | Rechazo 400 del backend → mortalidad/consumo/huevos vueltos a 0 en pantalla. |
| D3 | Tras un guardado **exitoso** el padre llama `showSuccessMessage()` y en el mismo instante cierra el modal: el diálogo «✅ Seguimiento Creado» nunca se ve y `showMessageModal` queda en `true`. | Al abrir «Nuevo registro» para el siguiente registro aparece ese aviso **encima del formulario en blanco** («creado exitosamente… todo en 0») y **Aceptar cierra el modal recién abierto**: hay que abrirlo otra vez. Con varios registros por día (Santa Reyes) es el pan de cada día. | `showMsg: true` al reabrir; clic en Aceptar → `isOpen: false`. Existe desde feb-2026 (`f5e8b89`); dolía poco mientras había un registro por día. |
| D4 | El botón Guardar **no espera** los tipos de huevo del lote: mientras `GET /LoteHuevoItem/{lote}` viaja (o falló) las filas están ocultas («Cargando…») y Guardar sigue habilitado. En Levante el tab «Huevos» directamente no existe mientras la consulta viaja. | Se guarda un día **sin huevos y sin ningún aviso**. | Con la consulta lenta: `cargando: true`, filas ocultas, `submitDisabled: false` (Producción); tabs `General/Stock` y botón habilitado (Levante). |
| D5 | El padre abre el modal con `loteId` = **id del LPP** (no el del lote base) hasta que responde `GET /LotePosturaProduccion/{id}`; el botón «Nuevo registro» ya está habilitado. | Silos y tipos de huevo responden `400`, no hay filas de huevo; al llegar el id correcto se vuelve a vaciar el formulario (D1). | Con la respuesta lenta: `loteId: 20`, `LoteSilo/20 → 400`, `LoteHuevoItem/20 → 400`, `errorHuevos: true`, 0 filas. |

Cómo se combinan en el campo: el operario abre el modal enseguida, teclea huevos, `informacion-lote` (o el id del
lote) llega y **borra**; vuelve a teclear lo de la pestaña General, guarda → mortalidad y consumo entran con huevos
en 0 («guarda el movimiento pero queda en 0»). Reabre: aparece el aviso viejo, Aceptar lo cierra, reabre otra vez,
carga solo los huevos, guarda; y como el primer renglón del día sigue mostrando 0 y el nuevo queda al pie, vuelve
a cargarlos (los dos renglones idénticos de la captura).

## Enfoque

Regla: el formulario abierto **no se toca** por datos que llegan tarde; cada dato tardío recarga *lo suyo*.

**Producción (`lote-produccion/`)**
1. `funciones/cambios-modal-seguimiento.funcion.ts` (pura + spec): dado el conjunto de `@Input` que cambiaron y si
   el modal se abre, decide `reiniciarFormulario` (solo al abrir o al cambiar el registro en edición),
   `recargarDatosDelLote` (`loteId`), `recargarInventario` (`granjaId`/`nucleoId`/`galponId`) y
   `recalcularEtapa` (`fechaEncaset`/`raza`, sin reiniciar). `loading` no hace nada.
2. `modal-seguimiento-diario.component.ts`: `ngOnChanges(changes)` usa esa función. Al abrir limpia
   `showMessageModal` (adiós al aviso viejo). Etapa y vigencia de primera postura se recalculan **sin** perder lo tecleado
   (`reconstruirFilasHuevo` ya conserva las cantidades por `catalogItemId`).
3. Guardar espera los tipos de huevo: `resolverGuardadoConHuevos` (pura, en `items-huevo-catalogo.funcion.ts`)
   devuelve `permitir | esperar | confirmar`. `esperar` deshabilita el botón y lo explica en el pie; `confirmar` (la consulta
   falló) pide confirmación explícita antes de guardar **sin huevos**; y el bloque de error trae «Reintentar» (ya no
   manda a cerrar y reabrir, que vaciaba todo). La consulta lleva `timeout` para que el estado nunca quede colgado.
4. `onSave()` ignora la llamada si ya hay un guardado en curso.
5. `lote-produccion-list`: el aviso de éxito pasa a `ToastService.success` (no diálogo); el modal recibe
   `loteIdBaseSeleccionado` (nunca el id del LPP) y «Nuevo registro» queda deshabilitado mientras se resuelve el
   lote base (`resolviendoLoteBase`); si esa consulta falla, aviso claro en vez de abrir con un id equivocado.

**Levante (`lote-levante/`)** — no tiene D1 (su `ngOnChanges` ya tiene la guarda) ni D3:
6. `modal-create-edit`: el mismo `resolverGuardadoConHuevos` (con la semana mínima y el modo por ítems):
   mientras los tipos del lote viajan, Guardar espera; si la consulta falló y el tab correspondería, confirma
   antes de guardar sin huevos.

## Archivos

- Nuevos: `lote-produccion/funciones/cambios-modal-seguimiento.funcion.ts` (+ `.spec.ts`),
  `lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.spec.ts`.
- Modificados: `lote-produccion/funciones/items-huevo-catalogo.funcion.ts` (+ spec),
  `modal-seguimiento-diario.component.ts/.html`, `lote-produccion-list.component.ts/.html`,
  `lote-levante/pages/modal-create-edit/modal-create-edit.component.ts/.html`.
- BD/SQL: ninguno. Backend: ninguno.

## Reglas de negocio que NO cambian

- Con la empresa en `permite_seguimiento_diario_parcial` (Santa Reyes) guardar un día sin huevos sigue siendo
  válido: solo se impide guardar **mientras los tipos no cargaron** y se pide confirmar si **fallaron**.
- El payload (`huevoItems`, totales, silos, consumo) no cambia; ningún cálculo se toca.
- Empresas sin `clasificacion_huevo_por_items`: el guardado por las 11 categorías queda idéntico. La guarda de
  `ngOnChanges` sí las beneficia (D1/D2/D3 no dependían del flag).

## Casos de prueba

Unitarios (Jasmine): `resolverCambiosModalSeguimiento` (abrir, editar otro registro, cada `@Input` tardío, `loading`
solo, modal cerrado); `resolverGuardadoConHuevos` (flag apagado, cargando, error, listo); componente: **un
`fechaEncaset`/`loteId`/`loading` tardío no borra lo tecleado**, abrir sí reinicia, Guardar deshabilitado mientras
cargan los tipos, `showMessageModal` limpio al abrir.

Navegador (back aislado + clon, Santa Reyes): repetir cada medición de la tabla (D1-D5) y comprobar el cambio;
alta con huevos + consumo → 201 con `huevo_tot` correcto; guardado con error → el formulario conserva lo tecleado;
reabrir tras guardar → sin aviso viejo; Levante: tab de huevos con la consulta lenta → Guardar espera.

Regresión: Sanmarino/Demo (flag apagado, 11 categorías) alta y edición sin cambios visibles.

## Riesgo / reversa

Solo front; revertir el commit basta. El riesgo está en que el modal ya no se «autolimpia» ante datos tardíos: por eso
cada `@Input` con efecto conserva su recarga puntual y hay spec de cada uno.
