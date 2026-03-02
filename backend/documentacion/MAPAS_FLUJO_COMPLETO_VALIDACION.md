# Módulo Mapas – Validación del flujo completo

## 1. Base de datos (PostgreSQL)

| Elemento | Estado | Detalle |
|----------|--------|---------|
| Tabla `mapa` | OK | `created_by_user_id` y `updated_by_user_id` tipo **UUID** (FK a `users.id`). |
| Tabla `mapa_paso` | OK | `mapa_id` INTEGER, FK a `mapa(id)`. |
| Tabla `mapa_ejecucion` | OK | `usuario_id` **UUID** (FK a `users.id`); `mensaje_estado`, `paso_actual`, `total_pasos` para progreso. |
| Scripts SQL | OK | `create_tablas_mapa.sql`, `add_mapa_ejecucion_mensaje_estado.sql`, `add_mapas_menu.sql`. |

## 2. Autenticación y UserGuid

| Elemento | Estado | Detalle |
|----------|--------|---------|
| JWT (AuthService) | OK | Incluye `ClaimTypes.NameIdentifier` y `sub` con `user.Id.ToString()` (Guid). |
| HttpCurrentUser | OK | Lee `NameIdentifier` o `sub`, hace `Guid.TryParse` y asigna a **UserGuid**. |
| MapasController | OK | Si falla por UserGuid (sesión sin Guid), responde **401 Unauthorized** en Create, Update, SavePasos y Ejecutar. |

## 3. Backend – Entidades y EF

| Elemento | Estado | Detalle |
|----------|--------|---------|
| `Mapa` | OK | No hereda `AuditableEntity`; tiene `CreatedByUserId` (Guid), `UpdatedByUserId` (Guid?). |
| `MapaEjecucion` | OK | `UsuarioId` tipo **Guid**. |
| MapaConfiguration | OK | Mapea columnas a UUID (Npgsql Guid ↔ uuid). |
| MapaEjecucionConfiguration | OK | `usuario_id` → Guid. |

## 4. Backend – Servicio (MapaService)

| Operación | Estado | Detalle |
|-----------|--------|---------|
| GetAllAsync | OK | Filtra por company, devuelve lista con totalEjecuciones y ultimaEjecucionAt. |
| GetByIdAsync | OK | Incluye pasos ordenados. |
| CreateAsync | OK | Usa `GetUserGuid()` para `CreatedByUserId`. |
| UpdateAsync | OK | Usa `GetUserGuid()` para `UpdatedByUserId`. |
| DeleteAsync | OK | Soft delete (`DeletedAt`). |
| SavePasosAsync | OK | Reemplaza pasos; usa `GetUserGuid()` para `UpdatedByUserId`. |
| EjecutarAsync | OK | Valida paso Export con SQL; crea ejecución; lanza proceso en background con `ProcessExecutionAsync`. |
| ProcessExecutionAsync | OK | Lee parámetros JSON, ejecuta pasos, actualiza `PasoActual`/`TotalPasos`/`MensajeEstado`; en error actualiza estado. |
| GetEjecucionEstadoAsync | OK | Devuelve estado, mensajeError, mensajeEstado, pasoActual, totalPasos, puedeDescargar. |
| GetEjecucionArchivoAsync | OK | Genera Excel desde `ResultadoJson`; nombre de archivo con timestamp. |
| GetEjecucionesByMapaAsync | OK | Historial por mapa (ordenado por fecha, limit). |
| Error en background | OK | Si `ProcessExecutionAsync` lanza, el `catch` del `Task.Run` actualiza la ejecución a estado "error" con el mensaje. |

## 5. Backend – API (MapasController)

| Método | Ruta | Estado |
|--------|------|--------|
| GET | /api/mapas | Lista mapas |
| GET | /api/mapas/{id} | Detalle mapa + pasos |
| GET | /api/mapas/{id}/ejecuciones?limit= | Historial ejecuciones |
| POST | /api/mapas | Crear mapa |
| PUT | /api/mapas/{id} | Actualizar mapa |
| DELETE | /api/mapas/{id} | Soft delete |
| PUT | /api/mapas/{id}/pasos | Guardar pasos |
| POST | /api/mapas/{id}/ejecutar | Lanzar ejecución (body: fechas, formatoExport) |
| GET | /api/mapas/ejecuciones/{ejecucionId} | Estado ejecución (polling) |
| GET | /api/mapas/ejecuciones/{ejecucionId}/descargar | Descargar Excel |

Rutas sin conflicto: `{id}/ejecuciones` es más específica que `{id}`; `ejecuciones/{ejecucionId}` es distinta.

## 6. Frontend – Rutas y menú

| Ruta | Componente | Estado |
|------|------------|--------|
| /mapas | Redirect a configuraciones | OK |
| /mapas/configuraciones | MapasConfiguracionesListComponent | OK |
| /mapas/configuraciones/:id | MapaConfigurarComponent | OK |
| /mapas/ejecutar/:id | MapaEjecutarPlaceholderComponent | OK |

Menú: `add_mapas_menu.sql` agrega ítem Mapas y submenús Configuraciones y Mapa.

## 7. Frontend – Servicio (mapas.service.ts)

| Método | Estado |
|--------|--------|
| getAll, getById, create, update, delete | OK |
| savePasos(mapaId, pasos) | OK |
| ejecutar(mapaId, request) | OK |
| getEjecucionEstado(ejecucionId) | OK |
| getEjecucionesByMapa(mapaId, limit) | OK |
| descargarEjecucion(ejecucionId) | OK | Devuelve `{ blob, fileName }` usando `Content-Disposition`. |

## 8. Frontend – Pantallas

| Pantalla | Funcionalidad | Estado |
|----------|----------------|--------|
| Configuraciones | Lista, crear/editar mapa (modal), enlaces Configurar / Ejecutar / Eliminar | OK |
| Configurar | Cargar mapa, listar pasos (tipo, etiqueta, SQL), añadir/quitar/reordenar, guardar | OK |
| Ejecutar | Parámetros (fechas, formato), validación paso Export, ejecutar, barra de progreso (pasoActual/totalPasos), polling, mensaje error, descarga, historial reciente con descarga por id | OK |

## 9. Flujo de ejecución end-to-end

1. Usuario abre **Ejecutar** para un mapa (con al menos un paso Export con SQL).
2. Completa fechas y formato → **Ejecutar**.
3. Frontend: POST `/api/mapas/{id}/ejecutar` → recibe `{ ejecucionId, estado: "en_proceso" }`.
4. Frontend: polling GET `/api/mapas/ejecuciones/{ejecucionId}` cada 2 s.
5. Backend: proceso en background actualiza `PasoActual`, `TotalPasos`, `MensajeEstado` y al terminar `Estado` = "completado" o "error".
6. Frontend: muestra barra de progreso; al completar muestra botón **Descargar**.
7. Descarga: GET `/api/mapas/ejecuciones/{id}/descargar` → blob + nombre desde `Content-Disposition`.

## 10. Correcciones aplicadas en esta validación

- **Controller**: En Create, Update, SavePasos y Ejecutar se captura `InvalidOperationException` cuando el mensaje indica falta de UserGuid y se responde **401 Unauthorized** para que el frontend pueda tratar sesión inválida o re-login.

---

**Requisito para que Mapas funcione:** El login debe emitir un JWT que incluya el Guid del usuario en el claim `sub` o `NameIdentifier` (ya implementado en AuthService). Así `HttpCurrentUser.UserGuid` queda asignado y las operaciones de mapas que usan `GetUserGuid()` no lanzan.
