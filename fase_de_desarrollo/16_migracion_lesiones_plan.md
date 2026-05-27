# 16_migracion_lesiones_plan

Fecha: 2026-05-27
Responsable: Equipo Frontend/Fullstack

Resumen
-------
Mover la funcionalidad de Lesiones desde el módulo "Lote Reproductora Aves de Engorde" al módulo "Seguimiento diario reproductora aves de engorde". Implementar botón superior +Registrar Lesión, tabs (Seguimiento Diario / Histórico de Lesiones), asegurar que el componente de lesiones escuche los filtros principales y que la creación/edición/eliminación refresque el histórico sin recargar la página.

Alcance técnico
---------------
- Frontend: Angular (mover/ajustar componentes, templates, servicios y rutas). 
- Backend: Revisar endpoints de lesiones para aceptar `loteReproductorId` y filtrar por cabecera.
- No se realizarán cambios en base de datos en esta tarea.

Archivos a modificar / crear
---------------------------
- Modificar: `frontend/src/app/features/lote-reproductora-ave-engorde/pages/lote-reproductora-ave-engorde-list/lote-reproductora-ave-engorde-list.component.html` (eliminar sección lesiones).
- Modificar: `frontend/src/app/features/seguimiento-diario/...` (añadir botón +Registrar Lesión en header).
- Mover/Modificar: `frontend/src/app/features/lesiones/*` (componentes: lista, formulario/modal, servicios) al módulo `seguimiento-diario`.
- Crear: `frontend/.../seguimiento-diario/lesiones-tabs/lesiones-tabs.component.ts/html` (tabs container si hace falta).
- Revisar: `backend/src/.../LesionesController.cs` y consultas para filtrar por `loteReproductorId`.

Tareas detalladas
-----------------
1) Preparación
   - [ ] Leer `CLAUDE.md` y seguir su workflow antes de tocar migraciones o endpoints.
   - [ ] Crear rama de trabajo `feat/lesiones-move-seguimientodiario`.

2) UI - Fase 1 (módulo origen)
   - [ ] Eliminar la tarjeta/section de Lesiones en `lote-reproductora-ave-engorde-list.component.html`.
   - [ ] Buscar y eliminar importaciones/estilos/servicios no usados en el módulo origen.

3) UI - Fase 2 (módulo destino)
   - [ ] Añadir botón `+ Registrar Lesión` en el header del módulo `seguimiento-diario`.
   - [ ] Crear/Adaptar `LesionesTabsComponent` con dos tabs: `Seguimiento Diario` y `Histórico de Lesiones`.
   - [ ] Importar y renderizar el componente de lista de lesiones en el Tab 2.
   - [ ] Al abrir el modal desde el nuevo botón, pre-cargar filtros (granja, núcleo, galpón, lote, reproductora).

4) Lógica
   - [ ] Hacer que el componente de lesiones escuche cambios en el filtro principal (`loteReproductorId`) usando un servicio compartido o `BehaviorSubject` del padre.
   - [ ] Implementar refresco automático del Tab 2 al cerrar modal de creación/edición/eliminación.
   - [ ] Deshabilitar botones y vistas si no hay `loteReproductorId` seleccionado.

5) Backend
   - [ ] Revisar endpoints de lesiones para confirmar parámetros de filtrado y seguridad.
   - [ ] Escribir pruebas manuales para validar que `GET /lesiones?loteReproductorId=...` devuelve solo registros relacionados.

6) QA y Deploy
   - [ ] Pruebas manuales: creación, edición, eliminación desde `Seguimiento Diario` → validar refresco y persistencia.
   - [ ] Revisión visual: validar que `Lote Reproductora` ya no muestre la tarjeta de lesiones.
   - [ ] Generar PR con descripción, capturas y pasos de QA.

Criterios de aceptación
----------------------
- La sección de lesiones ya no aparece en `Lote Reproductora Aves de Engorde`.
- El botón `+ Registrar Lesión` en `Seguimiento Diario` abre el modal con filtros heredados.
- Al crear/editar/eliminar se actualiza el `Histórico de Lesiones` automáticamente.
- Los filtros superiores persisten al cambiar entre tabs.

Notas operativas
----------------
- Seguir estrictamente el proceso descrito en `CLAUDE.md` (crear plan, limpiar `tracker_estado.md`, checklist con pasos) antes de tocar DB o migraciones.
- Realizar cambios por fases y PRs pequeños para facilitar revisión.


