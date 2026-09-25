# Gestión veterinaria — visitas, planes de tareas y evidencias

Fecha: 25-sep-2026
Estado: completado

## 1. Objetivo

Crear un módulo independiente de **Gestión veterinaria** que reutilice los patrones probados de
Implementación sin mezclar ambos dominios. El veterinario debe poder ver únicamente sus granjas y
ubicaciones asignadas, programar visitas, documentarlas y dejar tareas por granja, núcleo, galpón o
lote. Los usuarios con alcance sobre esa ubicación verán las tareas vigentes en el Inicio y podrán
cumplirlas desde una pantalla móvil, aportando observación y/o fotografía cuando el creador las haya
marcado como obligatorias.

Nombre de producto elegido: **Gestión veterinaria**. Dentro del módulo se usan las secciones
**Mis granjas**, **Agenda de visitas**, **Planes de tareas** y **Mis tareas**; así el nombre no limita
la evolución futura a un simple checklist.

## 2. Enfoque arquitectónico

- Módulo nuevo y cohesivo. No se agregan columnas veterinarias a `implementacion_*`: Implementación
  conserva su contrato de entrega/capacitación, firmas e ItalJira.
- Reutilización por patrón y servicios existentes: `ICurrentUser`, `user_farms`,
  `ILocationScopeResolver`, jerarquía granja→núcleo→galpón→lote, `ToastService`, panel compacto de
  Inicio y estilos/tokens Italfoods.
- Backend Clean Architecture:
  - entidades y estados en Domain;
  - DTOs, interfaz y reglas puras de autorización/fechas/evidencia en Application;
  - EF, consultas scoped y persistencia en Infrastructure;
  - controller REST delgado en API.
- Frontend Angular 22 standalone y lazy. Componentes con `changeDetection` explícito; páginas con
  estado mutable usan `ChangeDetectionStrategy.Eager`.
- El cumplimiento usa **página dedicada responsive** (`/gestion-veterinaria/tareas/:id/cumplir`), no
  modal: deja espacio para cámara, previsualización, observación, requisitos y mensajes de error.
- Fotografías comprimidas en el cliente y persistidas como evidencia independiente. Los listados
  sólo reciben metadata; el Base64 se obtiene on-demand para evitar respuestas pesadas.

## 3. Modelo de datos / migración EF

### `visitas_tecnicas`

- `id`, auditoría y `company_id`.
- ubicación: `farm_id` obligatorio; `nucleo_id`, `galpon_id`, `lote_id` opcionales.
- `titulo`, `objetivo`, `fecha_programada`, `fecha_realizada`, `observaciones`.
- `estado`: `programada | realizada | cancelada`.
- `veterinario_user_id`: Guid del usuario que agenda/realiza.

### `tareas_campo`

- `id`, auditoría y `company_id`; `visita_id` opcional para permitir tareas directas.
- ubicación: `farm_id` obligatorio; núcleo/galpón/lote opcionales.
- `titulo`, `instrucciones`, `fecha_inicio`, `fecha_fin`.
- requisitos: `requiere_observacion`, `requiere_foto`.
- `estado`: `pendiente | realizada | cancelada`.
- cumplimiento: `realizada_por_user_id`, `fecha_realizada`, `observacion_cumplimiento`.

### `tarea_campo_evidencias`

- `id`, `tarea_id`, metadata de archivo, contenido Base64 y autor/fecha.
- El contenido no se proyecta en listados.

La migración será idempotente (`CREATE TABLE/INDEX IF NOT EXISTS`) y sembrará el menú por `route`,
nunca por id fijo. No se aplica DDL a producción desde esta sesión.

## 4. Contratos y reglas de negocio

1. Toda lectura/escritura queda scoped por `company_id` de la empresa activa.
2. Una visita o tarea sólo puede apuntar a una granja asignada al usuario creador. La ubicación debe
   pertenecer a esa granja y respetar su alcance granular; ante ambigüedad se rechaza (fail-closed).
3. Una tarea es visible/cumplible por usuarios con `user_farms` para la granja y, si tienen alcance
   granular, sólo cuando el núcleo/galpón/lote de la tarea está permitido. Un usuario sin asignación
   explícita no recibe la tarea aunque conozca el id.
4. Una tarea puede ser general de granja o acotarse a núcleo, galpón o lote. La selección en cascada
   limpia descendientes incompatibles.
5. `fecha_inicio <= fecha_fin`; la tarea queda "próxima" antes de iniciar, "activa" dentro del rango
   y "vencida" después del fin mientras siga pendiente.
6. Para marcar `realizada`, el backend valida de nuevo los requisitos:
   - `requiere_observacion` ⇒ texto no vacío;
   - `requiere_foto` ⇒ al menos una imagen válida;
   - evidencia sólo JPEG/PNG/WebP y con tamaño acotado.
7. El Inicio muestra tareas pendientes activas/vencidas (y las próximas inmediatas) del usuario. Al
   completar correctamente desaparecen de esa bandeja, pero permanecen en el historial del módulo.
8. Cancelar/reabrir es auditable; no se borra evidencia de una tarea realizada.
9. Una visita realizada conserva observaciones y puede contener cero o más tareas de seguimiento.

## 5. API prevista

- `GET /api/GestionVeterinaria/mi-mapa` — granjas y jerarquía visible.
- `GET/POST/PUT /api/GestionVeterinaria/visitas`; acciones `realizar` y `cancelar`.
- `GET/POST/PUT /api/GestionVeterinaria/tareas`; acciones `cumplir`, `reabrir`, `cancelar`.
- `GET /api/GestionVeterinaria/mis-tareas` y `/mis-tareas/inicio`.
- `GET /api/GestionVeterinaria/tareas/{id}/evidencias` y evidencia individual on-demand.

## 6. Frontend

- Ruta lazy `/gestion-veterinaria` con dashboard visual:
  - cabecera contextual y próximos hitos;
  - tarjetas de granjas asignadas con ubicación y resumen núcleo/galpón/lote;
  - agenda de visitas;
  - tabla/timeline de tareas creadas y progreso;
  - acceso a Mis tareas.
- Formularios de visita y tarea con filtros en cascada y requisitos de cumplimiento visibles.
- Página `cumplir-tarea` mobile-first con `<input type="file" accept="image/*" capture="environment">`,
  previsualización, compresión y observación.
- Panel de Inicio compacto reutilizando la jerarquía de `pendientes-panel.scss`.
- Accesibilidad: labels, estados `aria-live`, foco visible, botones de cámara/carga con alternativa de
  archivo y ningún requisito comunicado sólo por color.

## 7. Archivos/componentes principales

- Backend:
  - `Domain/Entities/GestionVeterinaria/*`
  - `Application/DTOs/GestionVeterinaria/*`
  - `Application/Calculos/GestionVeterinariaCalculos.cs`
  - `Application/Interfaces/IGestionVeterinariaService.cs`
  - `Infrastructure/Persistence/Configurations/GestionVeterinaria/*`
  - `Infrastructure/Services/GestionVeterinaria/` (ancla partial + `Funciones/`)
  - `API/Controllers/GestionVeterinariaController.cs`
  - nueva migración EF + snapshot.
- Frontend:
  - `features/gestion-veterinaria/{models,services,funciones,components,pages,styles}`
  - ruta lazy en `app.config.ts`
  - panel nuevo en `features/home/`.

## 8. Casos de prueba

- Cálculos puros: fecha inválida, próxima/activa/vencida, requisitos de evidencia y autorización por
  ubicación (granja/núcleo/galpón/lote), incluyendo referencias nulas y fail-closed.
- Backend: usuario de otra empresa o granja no puede leer/crear/cumplir; alcance de núcleo/galpón/lote
  restringe correctamente; requisitos obligatorios se validan en servidor; completar es idempotente.
- Frontend: filtrado en cascada, validación de requisitos, compresión/metadata de imagen y estados de
  bandeja. Abrir/cerrar formularios dos veces y comprobar que ningún spinner queda congelado.
- Gates: `dotnet build`, `dotnet test`, `yarn build` y specs focalizadas si el runner está disponible.

## 9. Entrega y límites

- Esta iteración entrega el flujo completo local/repo y la migración lista para el pipeline.
- No crea usuarios concretos ni asigna el rol Veterinario a personas existentes: el módulo se siembra
  por ruta y la asignación real se hace desde Roles y Permisos.
- No se despliega ni se ejecuta DDL en producción sin aprobación explícita.
