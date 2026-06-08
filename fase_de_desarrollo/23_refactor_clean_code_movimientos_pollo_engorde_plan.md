# 23 — Refactor Clean Code: módulo `movimientos-pollo-engorde` (base multi-país)

> **Objetivo:** reorganizar el TypeScript del módulo aplicando *clean code*, extrayendo las
> funciones grandes asociadas a botones/acciones a una carpeta **`funciones/`** (una función por
> archivo), y centralizando tipos compartidos en **`models/`**. Esto deja un esqueleto limpio y
> fácil de navegar como **base previa** para la organización multi-país (modales por país que
> muestren datos distintos según el país).
>
> **Regla rectora:** *clean code sin cambiar comportamiento*. Es un refactor estructural; la UI,
> las llamadas HTTP y la lógica de negocio deben comportarse exactamente igual.

---

## 1. Estado actual (diagnóstico)

```
movimientos-pollo-engorde/
├── components/
│   ├── auditoria-ventas-modal/        (67 líneas TS — OK, ya es un componente chico)
│   └── modal-movimiento-pollo-engorde/ (902 líneas TS — God component)
├── pages/
│   └── movimientos-pollo-engorde-list/ (1353 líneas TS — God component)
├── services/
│   └── movimiento-pollo-engorde.service.ts (491 líneas — OK, queda igual)
└── movimientos-pollo-engorde-routing.module.ts
```

Problemas detectados:
1. **`movimientos-pollo-engorde-list.component.ts` = 1353 líneas.** Mezcla filtros, carga,
   agrupación de tabla, CRUD, acciones masivas (auditoría, organizar peso), export Excel y helpers.
2. **`resumenAuditoriaTexto` (líneas 191–299, ~108 líneas) es CÓDIGO MUERTO** → `private`, nunca
   se invoca (la auditoría hoy se muestra con `AuditoriaVentasModalComponent`). **Se elimina.**
3. **`formatearNumero` y `fechaCorta` duplicados** en list y modal → centralizar.
4. **`modal-movimiento-pollo-engorde.component.ts` = 902 líneas.** Construcción de DTOs, prorrateo
   de peso, agrupación por galpón y helpers mezclados con el ciclo de vida del componente.
5. Tipos compartidos (`FilaTablaMovimiento`, `LoteOption`, `VentaLineaGranja`, etc.) declarados
   *inline* dentro de los componentes → impiden reutilización limpia y crearían imports circulares
   al extraer funciones.

---

## 2. Estructura objetivo

```
movimientos-pollo-engorde/
├── models/                               ← NUEVO (tipos compartidos, multi-país friendly)
│   ├── movimiento-tabla.model.ts         · LoteOption, FilaDespachoGrupo, FilaMovimientoSimple, FilaTablaMovimiento
│   └── venta-granja.model.ts             · VentaLineaGranja, AvailableBirds, LoteDestinoOption, MovimientoPolloEngordeSaveDetail
├── funciones/                            ← NUEVO (una función "grande" por archivo)
│   ├── README.md                         · convención de la carpeta + nota multi-país
│   ├── formato.funcion.ts                · formatearNumero, fechaCorta, ymdToIsoUtcNoon  (dedupe)
│   ├── agrupar-despachos.funcion.ts      · construirFilasTabla + comparadores (tabla agrupada por despacho)
│   ├── exportar-ventas-excel.funcion.ts  · construir y descargar el Excel de ventas
│   ├── mapear-movimiento-dto.funcion.ts  · buildCreateDto / buildUpdateDto / buildVentaGranjaDespachoDto
│   └── prorateo-peso.funcion.ts          · calcularProrateoPreview + calcularProrateoTotales
├── components/
│   ├── auditoria-ventas-modal/           (sin cambios)
│   └── modal-movimiento-pollo-engorde/   (adelgazado, delega en funciones/ + models/)
├── pages/
│   └── movimientos-pollo-engorde-list/   (adelgazado, delega en funciones/ + models/)
├── services/
│   └── movimiento-pollo-engorde.service.ts (sin cambios)
└── movimientos-pollo-engorde-routing.module.ts (sin cambios)
```

### Por qué `models/` además de `funciones/`
Las funciones puras que extraemos (p. ej. `construirFilasTabla`) necesitan tipos como
`FilaTablaMovimiento`. Si esos tipos siguen declarados dentro del componente y la función vive en
`funciones/`, el componente importa la función y la función importa el tipo del componente →
**import circular**. Mover los tipos a `models/` rompe el ciclo y deja una capa de contratos
reutilizable por los futuros modales por país.

---

## 3. Criterio de extracción (qué entra a `funciones/` y qué se queda)

- **Entra a `funciones/`:** lógica **pura / sin estado** (sin `this`, sin DI). Recibe datos por
  parámetro y devuelve un resultado. Es testeable de forma aislada.
- **Se queda en el componente:** la orquestación delgada (estado del formulario/filtros, llamadas
  a `service`, `toastService`, banderas de carga, abrir/cerrar modales). El handler del botón pasa
  a ser un *wrapper* corto que arma los parámetros y llama a la función pura.

Esto preserva la delicada lógica de *change detection* (referencias estables de
`lotesParaVentaGranjaList` y `gruposVentaPorGalpon`; ver comentarios en el código actual) sin
tocar su comportamiento.

---

## 4. Detalle por archivo

### `funciones/formato.funcion.ts`
- `formatearNumero(n: number): string` — `Intl.NumberFormat('es-CO')`.
- `fechaCorta(iso): string` — `toLocaleDateString('es')` con guardas.
- `ymdToIsoUtcNoon(ymd): string | null` — `YYYY-MM-DD` → ISO mediodía UTC (mover desde el modal).
- Reemplaza las copias duplicadas en list y modal (que pasan a delegar o re-exportar).

### `funciones/agrupar-despachos.funcion.ts`
- `construirFilasTabla(list, opciones?): FilaTablaMovimiento[]` (antes `buildFilasTabla`).
- Incluye los comparadores puros `fechaDiaISO`, `compareMovimientoFechaDesc`,
  `compareFilaTablaDesc` (privados del archivo).
- Tipos importados desde `models/movimiento-tabla.model.ts`.

### `funciones/exportar-ventas-excel.funcion.ts`
- `exportarVentasExcel(rows, meta)` donde `meta = { granjaNombre, galponNombre?, loteNombre?, filtros }`.
- Encapsula headers, mapeo de filas, título, hoja y `XLSX.writeFile`.
- El componente solo valida "hay filas", arma `meta`, llama la función y muestra el toast.

### `funciones/mapear-movimiento-dto.funcion.ts`
- `buildCreateDto(formValue, ctx)`, `buildUpdateDto(formValue)`,
  `buildVentaGranjaDespachoDto(formValue, ctx)`.
- `ctx` lleva lo que hoy sale de `this` (origen parseado, `usuarioMovimientoId`, `lotesVentaGranja`,
  `ventaLineasGranja`, `permitirSobrante`). Funciones puras → devuelven el DTO o `null`.

### `funciones/prorateo-peso.funcion.ts`
- `calcularProrateoPreview(lineas, pesoBruto, pesoTara)` (espejo del backend con ajuste de residuo).
- `calcularProrateoTotales(rows)`.
- Reemplaza los getters `prorateoPreview` / `prorateoTotales` del modal (que pasan a delegar).

### `models/movimiento-tabla.model.ts`
- `LoteOption`, `FilaDespachoGrupo`, `FilaMovimientoSimple`, `FilaTablaMovimiento`.

### `models/venta-granja.model.ts`
- `VentaLineaGranja`, `AvailableBirds`, `LoteDestinoOption`, `MovimientoPolloEngordeSaveDetail`.
- Para no romper imports existentes, los componentes **re-exportan** estos tipos
  (`export type { ... } from '../../models/...'`).

---

## 5. Invariantes que NO se deben romper (verificación obligatoria)

- [ ] La plantilla del list sigue resolviendo TODOS sus métodos/getters públicos
      (`descargarExcel`, `auditarVentas`, `completarGrupoDespacho`, `formatearNumero`, etc.).
- [ ] La plantilla del modal sigue resolviendo `prorateoPreview`, `prorateoTotales`,
      `formatearNumero`, `fechaCorta`, etc. (se mantienen como getters/métodos que delegan).
- [ ] Referencias estables de `lotesParaVentaGranjaList` y `gruposVentaPorGalpon` intactas
      (no convertir en getters que recalculan en cada CD).
- [ ] El export `MovimientoPolloEngordeSaveDetail` sigue importable desde el modal
      (re-export para no tocar el list).
- [ ] La firma de eventos `@Output() save`/`close` y los `@Input()` del modal no cambian.
- [ ] `dotnet`/backend: sin cambios (refactor 100% frontend).

---

## 6. Validación

1. `cd frontend && yarn build` (o `yarn ng build`) → debe compilar sin errores de tipos.
2. Revisión manual de que no quedan imports rotos ni símbolos sin usar.
3. (Opcional) `yarn test` de los specs existentes si los hubiera para el módulo.
4. Detener cualquier proceso/servidor que se levante para validar (disciplina de ciclo de vida).

---

## 7. Fuera de alcance (siguiente fase, multi-país)

- Crear modales por país y la lógica condicional por país.
- Esa fase reutilizará `funciones/` y `models/` como base. Aquí solo dejamos el terreno limpio.
