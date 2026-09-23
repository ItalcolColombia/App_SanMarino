# Plan — Seguimiento diario de Levante: detalle responsive y validación visible

## Objetivo

Replicar en Seguimiento Diario de Levante las mejoras de usabilidad ya validadas en Producción, sin cambiar contratos, cálculos ni reglas de negocio: tabla operable desde escritorio, tablet y celular; estado de validación inequívoco; acción de validar accesible; y modal de detalle organizado por secciones táctiles.

## Enfoque arquitectónico

- Alcance exclusivamente frontend en el feature `lote-levante`.
- Mantener `TabsPrincipalComponent` como orquestador del estado y agregar solo comportamiento de presentación para sincronizar las barras de desplazamiento.
- Conservar `ModalDetalleSeguimientoLevanteComponent` con `ChangeDetectionStrategy.Eager` y estado mutable explícito; la pestaña activa será una referencia estable, no un getter que cree objetos por ciclo.
- Reutilizar los tokens del sistema visual del repositorio. No se modifican endpoints, DTOs, persistencia, cálculos ni migraciones.
- Preservar los flags multiempresa existentes (`ocultaMachosEnPostura`, agua Ecuador/Panamá y huevos en Levante) con comportamiento fail-closed actual.

## Archivos a modificar o crear

- `frontend/src/app/features/lote-levante/pages/tabs-principal/tabs-principal.component.ts`
  - Medición del ancho real de la tabla, sincronización de scroll superior/inferior y clase visual para filas pendientes.
- `frontend/src/app/features/lote-levante/pages/tabs-principal/tabs-principal.component.html`
  - Leyenda de estados, barra horizontal superior, columna de acciones fija y botón Validar con texto.
- `frontend/src/app/features/lote-levante/pages/tabs-principal/tabs-principal.component.scss`
  - Estados completos de fila, columna fija, tamaños táctiles y adaptación responsive sin comprimir columnas.
- `frontend/src/app/features/lote-levante/pages/modal-detalle-seguimiento/modal-detalle-seguimiento.component.ts`
  - Estado de navegación del detalle y reinicio seguro al abrir/cerrar registros.
- `frontend/src/app/features/lote-levante/pages/modal-detalle-seguimiento/modal-detalle-seguimiento.component.html`
  - Resumen y pestañas accesibles para General, Aves, Ítems, Nutrición/Agua y Huevos.
- `frontend/src/app/features/lote-levante/pages/modal-detalle-seguimiento/modal-detalle-seguimiento.component.scss`
  - Modal responsive de pantalla completa en celular, navegación táctil y jerarquía visual.
- Pruebas unitarias enfocadas del listado y del modal en sus carpetas correspondientes.

## Base de datos / SQL

Sin cambios. No se crean columnas, migraciones, vistas, funciones ni seeds.

## Reglas de negocio que se preservan

- `PENDIENTE`, `VALIDADO` y `EN_RETRASO` siguen proviniendo del contrato actual; solo cambia su representación visual.
- La acción Validar se muestra únicamente cuando `puedeValidarFila(...)` ya lo permite.
- El detalle mantiene todos los campos actuales: generales, hembras, machos, ítems por sexo y generales, agua, nutrición y huevos.
- Las empresas con ocultación de machos no reciben controles ni contenido de machos.
- Los datos de agua continúan condicionados a Ecuador/Panamá.
- Abrir el detalle no modifica ni confirma el registro.

## Casos de prueba

- Una fila `PENDIENTE` recibe estilo gris/neutro y una fila `EN_RETRASO` conserva alarma roja.
- El botón Validar incluye etiqueta visible y solo aparece en filas validables.
- La barra superior mueve la tabla y el scroll de la tabla actualiza la barra superior.
- La columna Acciones permanece visible al desplazar horizontalmente.
- El modal inicia siempre en General al abrir un registro.
- La navegación cambia entre pestañas y conserva los datos propios de Levante.
- El modal oculta Machos según flag y no ofrece secciones inválidas.
- En viewport móvil, los objetivos táctiles y el cierre permanecen visibles.
- Ejecutar pruebas unitarias enfocadas del módulo y `yarn build` sin errores nuevos.
