# `cuadres-offline/funciones/` — reglas puras de la bandeja de cuadre

Convención de CLAUDE.md ("CLEAN CODE — organización de funciones"): una función **pura** por
responsabilidad, sin `this`, sin DI, sin `HttpClient`. La página es un orquestador delgado: pide los
datos, llama a estas funciones y maneja HTTP/UI.

## `etiquetarTipoCuadre`

Traduce el `tipo` del contrato de sync (`seguimiento_levante_crear`) al nombre que entiende un
supervisor de granja.

**El caso que importa es el tipo desconocido.** El identificador del contrato es estable a propósito
—el cliente decide con él y cambiarlo rompe dispositivos ya instalados—, así que un servidor más
nuevo puede mandar uno que este cliente no mapea. La función devuelve entonces **el identificador
crudo**, nunca `undefined` ni «Desconocido»: una fila sin nombre no se puede reportar a soporte, y es
justamente cuando algo raro pasó que hace falta poder reportarla.

Reusable en cualquier país/empresa: el mapa es del contrato, no del tenant.
