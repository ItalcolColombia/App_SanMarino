# Seguimiento diario Produccion: validacion visible y detalle responsive

Fecha: 22-sep-2026. Alcance: frontend de Postura Produccion, con foco operativo en Santa Reyes.
Sin cambios de BD, SQL, endpoints ni contratos.

## Diagnostico de persistencia

El incidente «los huevos quedan en 0» ya fue reproducido y corregido en los commits `4f71cbe` y
`d2543c2`, ambos contenidos en `origin/main-produccion`:

- el formulario se reiniciaba cuando llegaban tarde datos del lote o cambiaba `loading`;
- Guardar podia ejecutarse antes de terminar de cargar los tipos de huevo del lote;
- el backend persistia correctamente `metadata.huevoItems` cuando el request lo incluia;
- la cola offline/PWA conservaba `huevoItems`; no era una perdida de red;
- la primera fila de un dia con varios registros podia mostrar 0 aunque otras filas del mismo dia si
  tuvieran huevos; ahora muestra el total diario y el toast resume lo guardado.

Esta tarea no vuelve a implementar ese arreglo: lo conserva y lo revalida con sus pruebas de regresion.
La Task Definition viva no se pudo certificar: `aws ecs describe-services` respondio
`UnrecognizedClientException` porque el token local esta vencido. Estar en la rama remota no se toma
como prueba de despliegue efectivo.

## Enfoque de interfaz

### Tabla de registros

1. Mantener la columna Acciones fija a la derecha y aumentar los objetivos tactiles.
2. Agregar una barra de desplazamiento horizontal superior, sincronizada con la tabla, para revisar
   columnas sin bajar hasta el final del contenedor.
3. No comprimir las 30+ columnas en tablet/celular: conservar su ancho legible y desplazar la tabla.
4. Pintar la fila `PENDIENTE` con fondo gris neutro y conservar `EN_RETRASO` en rojo. Mostrar una
   leyenda de estados cuando la empresa usa doble validacion.
5. Hacer explicito el boton «Validar» en lugar de dejar solo el simbolo de verificacion.

### Modal de detalle

1. Reemplazar los radio-tabs pequenos por botones de pestana accesibles, con objetivos tactiles de al
   menos 44 px y estado activo explicito.
2. Agrupar Hembras/Machos en una sola pestana «Aves» para reducir navegacion; el flag
   `ocultaMachosEnPostura` sigue retirando por completo los datos de machos para Santa Reyes.
3. Agregar un resumen superior (fecha, id y huevos) para reconocer el registro sin cambiar de pestana.
4. Eliminar el scroll vertical anidado de los paneles: solo desplaza el cuerpo del modal; cabecera,
   pestanas y pie quedan accesibles.
5. En tablet usar grillas de dos columnas y en celular una; no cambia ningun valor ni calculo.

## Archivos

- `frontend/src/app/features/lote-produccion/pages/tabs-principal/tabs-principal.component.{ts,html,scss,spec.ts}`
- `frontend/src/app/features/lote-produccion/pages/modal-detalle-seguimiento/modal-detalle-seguimiento.component.{ts,html,scss}`
- Nuevo spec del modal de detalle en la misma carpeta.

## Reglas de negocio preservadas

- Clasificacion por items solo cuando `clasificacionHuevoPorItems` esta activa; Sanmarino conserva las
  11 categorias fijas.
- Santa Reyes sigue ocultando machos por `ocultaMachosEnPostura`.
- Ausencia en `estadoValidacionPorId` significa `VALIDADO`; `PENDIENTE` y `EN_RETRASO` no cambian su
  semantica ni el endpoint de validacion.
- Validar, editar, eliminar, exportar y abrir detalle emiten exactamente los mismos eventos.
- La presentacion responsive aplica a todas las empresas, pero los estados solo aparecen cuando
  `requiereValidacion` esta activo.

## Casos de prueba

- Tabla: fila pendiente recibe clase neutra, fila vencida conserva clase roja; columna Estado y
  Acciones siguen alineadas; el boton de validacion mantiene el mismo evento.
- Scroll: mover la barra superior mueve la tabla y mover la tabla actualiza la barra superior.
- Detalle: abre en General; cambio a Aves/Huevos; al reabrir vuelve a General; Santa Reyes no renderiza
  machos; clasificacion por items sigue leyendo `metadata.huevoItems`.
- Regresion del guardado: correr los specs de `modal-seguimiento-diario`,
  `cambios-modal-seguimiento`, `items-huevo-catalogo`, `filas-grilla-produccion` y
  `resumen-guardado-seguimiento`.
- Gate final: `cd frontend && yarn build`.

## Resultado

- Tabla: barra superior sincronizada con el ancho real renderizado; Acciones permanece fija, el boton
  dice «Validar» y los objetivos tactiles miden al menos 36 px dentro de la grilla. En celular el ancho
  de columnas no se comprime.
- Estados: `PENDIENTE` aplica la clase neutra y pinta toda la fila gris; `EN_RETRASO` conserva el rojo;
  la leyenda solo aparece cuando `requiereValidacion` esta activo.
- Detalle: cinco pestanas de 46-48 px, Aves unificada, resumen visible, pie fijo y un solo scroll. A
  900 px baja a dos columnas y a 640 px ocupa la pantalla con grilla de una columna. Los datos de
  machos siguen ausentes cuando `ocultaMachosEnPostura` esta activo.
- Tests de tabla/modal: 13/13. Regresion completa del flujo de huevos + UI tocada: 98/98.
- `yarn build` final con Node 22.23.1: exitoso, 0 errores y 0 advertencias Angular (solo el aviso
  preexistente de Yarn por el `package.json` padre sin licencia).
- Sin cambios de backend, BD, SQL, endpoints ni payloads.
