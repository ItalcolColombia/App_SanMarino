# 16_migracion_lesiones_plan.md

Objetivo
-------
Mover la funcionalidad de Lesiones desde el módulo "Lote Reproductora Aves de Engorde" hacia "Seguimiento diario reproductora aves de engorde".

Alcance
-------
- Quitar UI de Lesiones del módulo origen.
- Añadir botón `+ Registrar Lesión` en la cabecera del módulo destino.
- Incorporar pestañas: `Seguimiento Diario` y `Histórico de Lesiones`.
- Mover componentes/modales/listados de Lesiones y asegurar que escuchen los filtros existentes (granja, núcleo, galpón, lote/reproductora).
- Asegurar refresco automático del histórico tras crear/editar/eliminar lesiones.
- Verificar y, si es necesario, crear migraciones backend para persistir cualquier campo faltante (por ejemplo `loteReproductorId` en registros de lesiones).

Fases y tareas (detallado)
---------------------------

Fase 0 — Preparación (local)
- Crear rama: `feat/lesiones-move-seguimientodiario`.
- Ejecutar `yarn build` en frontend y `dotnet build` en backend para detectar errores actuales.

Fase 1 — Remoción en origen
- Archivos objetivo (frontend):
  - `frontend/src/app/features/lote-reproductora-ave-engorde/pages/lote-reproductora-ave-engorde-list/lote-reproductora-ave-engorde-list.component.html`
  - `frontend/src/app/features/lote-reproductora-ave-engorde/pages/lote-reproductora-ave-engorde-list/lote-reproductora-ave-engorde-list.component.ts`
- Pasos:
  1. Localizar la sección de Lesiones (tabla, botones, importaciones y servicios usados exclusivamente por esa sección).
  2. Extraer (copiar) los componentes pertinentes a una carpeta temporal o anotar sus rutas para moverlos.
  3. Eliminar la sección y limpiar imports no usados. Ejecutar `yarn build` para validar.

Fase 2 — Integración en destino (Seguimiento diario reproductora aves de engorde)
- Archivos objetivo (frontend):
  - Identificar el componente cabecera del módulo Seguimiento diario (ej: `frontend/src/app/features/seguimiento-diario/.../header.component.ts` o similar). Si no existe, usar el componente principal del listado.
  - Carpeta destino para Lesiones: `frontend/src/app/features/seguimiento-diario/components/lesiones/`
- Pasos:
  1. Copiar/mover componentes de Lesiones a la carpeta destino y ajustar rutas/exports.
  2. Añadir botón `+ Registrar Lesión` en la cabecera; botón debe abrir el modal existente de registro de lesiones y pasar filtros actuales.
  3. Implementar un contenedor con pestañas: primera pestaña mantiene la UI de Seguimiento diario; segunda pestaña muestra el `Histórico de Lesiones` (componente movido).
  4. Asegurar que el componente Lesiones reciba como @Input o use un servicio compartido los filtros activos (granja/núcleo/galpón/loteReproductorId).
  5. Implementar un EventEmitter o Subject para notificar operaciones (create/update/delete) y así refrescar el histórico automáticamente.

Fase 3 — Backend: verificación y migraciones
- Objetivos:
  - Confirmar que la entidad y endpoints de Lesiones soportan filtrado por `loteReproductorId` y que los modelos contienen los campos necesarios.
  - Si faltan campos en la tabla `lesiones` o si la relación con `lote_reproductora` no existe, crear migración EF Core idempotente.
- Pasos:
  1. Revisar: `backend/src/ZooSanMarino.Infrastructure/Entities` y `Controllers` relacionados con Lesiones.
  2. Ejecutar localmente queries para inspeccionar esquema: verificar columnas `lote_reproductor_id`, `fecha`, `tipo`, `cantidad`, etc.
  3. Si falta `lote_reproductor_id` o una FK necesaria, crear migración:
     - `dotnet ef migrations add AddLoteReproductorIdToLesiones --project ../ZooSanMarino.Infrastructure --startup-project . --context ZooSanMarinoContext`
     - Editar `Up()` para usar `migrationBuilder.Sql("ALTER TABLE ... ADD COLUMN IF NOT EXISTS ...")` para idempotencia cuando aplique.
  4. Probar `dotnet ef database update` contra la BD local (docker) y validar que la API responde correctamente al filtrar por `loteReproductorId`.

Fase 4 — Pruebas y QA
- Ejecutar `yarn build` en frontend y `dotnet build` en backend.
- Probar en navegador:
  - Abrir `Seguimiento diario reproductora aves de engorde`.
  - Verificar botón `+ Registrar Lesión` abre modal con filtros aplicados.
  - Crear lesión → confirmar que Histórico se actualiza automáticamente.
  - Editar/Borrar lesión → confirmar refresco.
  - Confirmar que la sección ya no aparece en `Lote Reproductora Aves de Engorde`.

Fase 5 — Revisión y PR
- Crear PR pequeño por fase (idealmente: Fase1 PR, Fase2 PR, Fase3 PR).
- Describir en PR los cambios, archivos movidos, y migraciones aplicadas.

Notas técnicas y consideraciones
--------------------------------
- Mantener cambios pequeños y revisables para evitar regresiones en el módulo Seguimiento.
- When moving components, preserve tests (if any) and update import paths.
- Use Angular lazy-loading imports if the Lesiones components are heavy.
- On backend migrations: prefer idempotent SQL in `migrationBuilder.Sql(...)` to be safe in prod.

Archivos que probablemente tocarás
- Frontend:
  - `frontend/src/app/features/lote-reproductora-ave-engorde/pages/lote-reproductora-ave-engorde-list/*`
  - `frontend/src/app/features/seguimiento-diario/**` (header, list, filters)
  - `frontend/src/app/features/lesiones/**` (mover/copiar)
- Backend:
  - `backend/src/ZooSanMarino.Infrastructure/Entities/Lesion.cs` (o equivalente)
  - `backend/src/ZooSanMarino.API/Controllers/LesionesController.cs`
  - `backend/src/ZooSanMarino.Infrastructure/Migrations/` (nueva migración si se necesita)

Checklist de aceptación
----------------------
- [ ] La UI de Lesiones ya no aparece en `Lote Reproductora Aves de Engorde`.
- [ ] Existe botón `+ Registrar Lesión` en la cabecera de `Seguimiento diario reproductora aves de engorde`.
- [ ] Histórico de Lesiones visible en pestaña y responde a filtros del seguimiento.
- [ ] Crear/Editar/Borrar lesiones refresca histórico automáticamente.
- [ ] Backend filtra por `loteReproductorId` y migraciones aplicadas correctamente en local.

Plan de rollback
----------------
- Revertir PRs por fase si algo falla.
- Para migraciones, crear script SQL de rollback si la migración no es trivial.

Tiempo estimado
---------------
- Fase 1: 1–2 horas
- Fase 2: 3–6 horas
- Fase 3: 1–3 horas (depende de si se necesita migración)
- Fase 4: 1–2 horas
- Total: 6–13 horas (estimado)

---
Fecha: 2026-05-27
Autor: Equipo de desarrollo
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


