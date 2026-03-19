# Inventario y Seguimiento Diario (alineación por módulo)

Los módulos de inventario están **divididos** según el tipo de seguimiento:

| Módulo | Inventario que usa | Descuento / devolución |
|--------|--------------------|-------------------------|
| **Seguimiento diario postura** (ProduccionService, `seguimiento_diario` tipo producción) | No aplica inventario nuevo (inventario-gestion). Puede usar otro flujo de inventario propio del módulo postura. | No se realizan descuentos ni devoluciones sobre inventario-gestion. |
| **Seguimiento diario pollo de engorde** (SeguimientoAvesEngordeService, `seguimiento_diario_aves_engorde`) | **Inventario nuevo** (inventario-gestion / item_inventario_ecuador). | Crear → descuento. Editar → ajuste (más consumo o devolución). Eliminar → devolución total. |

- **ProduccionService**: no inyecta ni usa `IInventarioGestionService`. Crear/actualizar/eliminar seguimiento de postura no modifica inventario-gestion.
- **SeguimientoAvesEngordeService**: inyecta `IInventarioGestionService` (opcional) y aplica la lógica de consumo y devolución solo para el módulo pollo de engorde.

Referencia: tablas `inventario_gestion_stock`, `inventario_gestion_movimiento`, catálogo `item_inventario_ecuador` (Ecuador/Panamá).
