# Módulo Vacunación — cronogramas por lote/granja/galpón

## Contexto

Pedido nuevo del usuario (sesión origen archivada sin código, ver `fase_de_desarrollo/CONTEXTO_TRASPASO_VACUNACION.md`): un módulo para programar y controlar el cronograma de vacunación de un lote durante toda su vida, con dos perfiles (administrador de cronograma / operador que registra aplicación) y un módulo de reportes/gráficas de cumplimiento. No existe BD para esto — se crea desde cero. El usuario confirmó explícitamente que **debe ser genérico**: cubre tanto Postura (Levante+Producción, se programa por semana) como Pollo Engorde (ciclo corto, se programa por día/edad), y debe quedar abierto a líneas futuras sin rediseño de esquema.

Decisiones ya confirmadas por el usuario (`AskUserQuestion`):
- Catálogo de vacunas: filtrar `ItemInventario` por `TipoItem = "vacuna"` (sin tabla nueva).
- Rol: **no** se crea rol nuevo; se siembran permisos `vacunacion.*` nuevos que un admin asigna a roles existentes vía UI de Roles.
- Umbral de "incumplido en rojo": **configurable por empresa/país** (no hardcode).
- Exportables Excel v1: cronograma completo, historial de aplicaciones y reporte de cumplimiento comparativo — los tres.
- Cronograma: **uno solo por lote**, con franjas de tiempo genéricas (semana en Postura, día/edad en Engorde), o fecha fija sin importar la fase.
- Aplicar antes de que abra la franja: permitido, mismo tratamiento que aplicar tarde (exige descripción).
- Métricas de cumplimiento: % a tiempo, % tardío (desglosado 1 semana / 2+ semanas), % no aplicado, promedio de días de atraso.
- "Aplicado por": selector de usuarios del sistema + opción "otro" con texto libre.

## Hallazgos clave de la investigación (por qué el diseño es así)

- **No hay un `Lote` único compartido de forma confiable.** `Lote` (tabla `lotes`, PK `lote_id`) es el modelo legacy; `LotePosturaLevante`, `LotePosturaProduccion` y `LoteAveEngorde` son tablas independientes con su propio PK y su propia `FechaEncaset`, cada una con un `LoteId` nullable "de compatibilidad" hacia `lotes`. No se pudo verificar en esta fase (sin acceso a la BD local en este momento) qué tan poblado está ese `LoteId` en filas reales. **Por eso el ancla del cronograma no es "un Lote", sino un discriminador `LineaProductiva` + FK específico por línea** (mismo patrón ya usado en `seguimiento_diario_levante`, que tiene `lote_id` legacy + `lote_postura_levante_id` nullable — `SeguimientoDiarioConfiguration.cs:17-19`).
- **Catálogo de vacunas**: `ItemInventario.TipoItem` (string libre) ya es filtrable por query (`ItemInventarioService.cs:41`). Se usa `TipoItem = "vacuna"`.
- **Visibilidad por granja**: reusar `IFarmService.GetAssignedFarmsForCompanyAsync(userGuid, companyId, paisId?)` (`FarmService.cs:244-269`), mismo patrón que `LoteFormDataService.cs:65-67`.
- **Cálculo de edad/semana**: no hay un helper único compartido hoy (`SeguimientoEngordeCalculos.CalcularSemana` usa `floor+1`; `ReporteTecnicoService.CalcularEdadDias/Semanas` usa `+1` y `ceiling`). El módulo de vacunación define **su propio cálculo puro** en `Application/Calculos/VacunacionCalculos.cs`, sin reusar ninguno de los dos (evita heredar la inconsistencia existente).
- **Relación lote→galpón**: FK directo `GranjaId`/`NucleoId`/`GalponId` en cada tabla de lote (no hay tabla puente para Postura/Engorde), configurado vía `LoteConfiguration.cs:88-99`.
- **Patrón de permisos**: sembrado por migración de solo datos (`INSERT ... WHERE NOT EXISTS`), ejemplo real reciente: `AddPermisoVentaLotesCerradosMovimientoPolloEngorde.cs`. Roles/asignación se maneja aparte, no se hardcodea.
- **Plantilla SQL para reportes**: `backend/sql/fn_informe_semanal_pollo_engorde.sql` (una fila por lote×semana con filtros `p_company_id, p_granja_ids[], p_nucleo_id, p_galpon_id, p_lote_id, p_fecha_desde/hasta`) y `fn_indicadores_pollo_engorde.sql` (agregado por lote) son las plantillas a copiar para las funciones de cumplimiento.

## Modelo de datos (nuevo, migraciones EF idempotentes)

### `VacunacionCronogramaItem` (tabla `vacunacion_cronograma_item`) — el plan armado por el administrador
- `Id`, `CompanyId`, `PaisId`
- `LineaProductiva` (string: `"Levante"` | `"Produccion"` | `"Engorde"`, extensible a futuro sin migración de schema)
- `LotePosturaLevanteId` (int?, FK), `LotePosturaProduccionId` (int?, FK), `LoteAveEngordeId` (int?, FK) — exactamente uno poblado según `LineaProductiva` (constraint a nivel aplicación, igual que el patrón de `seguimiento_diario_levante`)
- `GranjaId`, `NucleoId`, `GalponId` — denormalizados al crear (mismo patrón que las tablas de lote), para filtros de reportes sin join extra
- `ItemInventarioId` (FK → `ItemInventario`, filtrado `TipoItem = "vacuna"` en el selector)
- `UnidadObjetivo` (string: `"Semana"` | `"Dia"` | `"Fecha"`)
- `ValorObjetivo` (int?) — semana N o día N (edad) según `UnidadObjetivo = Semana/Dia`
- `FechaObjetivo` (date?) — usado solo si `UnidadObjetivo = "Fecha"`
- `RangoDiasAntes`, `RangoDiasDespues` (int, default 0) — ancho de la franja válida (ej. semana = 6/0 para que la franja sea lunes-domingo relativo al día objetivo; engorde puede ser una franja más angosta)
- `Orden` (int), `Activo` (bool), `Notas` (string?)
- Auditoría: `CreatedAt`, `CreatedByUserId`, `UpdatedAt`, `UpdatedByUserId`

### `VacunacionRegistroAplicacion` (tabla `vacunacion_registro_aplicacion`) — lo que registra el operador, 1:1 con el ítem de cronograma
- `Id`, `VacunacionCronogramaItemId` (FK único)
- `Estado` (string: `"Pendiente"` | `"Aplicado"` | `"AplicadoTardio"` | `"AplicadoAdelantado"` | `"NoAplicado"`)
- `FechaAplicacion` (date?) — **siempre `DateTime.UtcNow`/fecha de servidor al confirmar**, nunca editable por el usuario
- `DiasDesviacion` (int?) — calculado (positivo = tarde, negativo = adelantado) respecto al borde más cercano de la franja
- `Incumplido` (bool) — `true` si `DiasDesviacion` supera el umbral configurable de la empresa
- `MotivoDescripcion` (string?) — **obligatorio** (validado en el handler, no solo en el front) si `Estado = NoAplicado` o si hay desviación fuera de franja
- `UsuarioRegistraId` (FK, obligatorio — usuario logueado que hace el registro)
- `AplicadoPorUserId` (FK?, nullable), `AplicadoPorNombreLibre` (string?, nullable) — constraint: exactamente uno de los dos poblado
- `CompanyId`, `PaisId`
- Auditoría estándar

### `VacunacionConfiguracion` (tabla `vacunacion_configuracion`) — umbral configurable
- `CompanyId` (PK), `PaisId` (PK compuesta si aplica multi-país por empresa)
- `DiasUmbralIncumplido` (int, default 14 — cubre el "1-2 semanas" mencionado por el usuario como default razonable, editable por empresa)

Todas las migraciones siguen el flujo estándar: `dotnet ef migrations add <Nombre> --project ../ZooSanMarino.Infrastructure --startup-project . --context ZooSanMarinoContext`, con DDL idempotente (`CREATE TABLE IF NOT EXISTS` vía `Sql()` si hace falta, aunque `migrationBuilder.CreateTable` ya es seguro para tablas nuevas). Falta además la migración de datos que siembra los permisos (ver más abajo).

## Backend — estructura (Clean Architecture, patrón `movimientos-pollo-engorde`)

```
Domain/Entities/Vacunacion/
├── VacunacionCronogramaItem.cs
├── VacunacionRegistroAplicacion.cs
└── VacunacionConfiguracion.cs

Infrastructure/Configurations/Vacunacion/   (EF configs, snake_case por convención)
├── VacunacionCronogramaItemConfiguration.cs
├── VacunacionRegistroAplicacionConfiguration.cs
└── VacunacionConfiguracionConfiguration.cs

Infrastructure/Services/Vacunacion/
├── VacunacionCronogramaService.cs           # ancla: usings, ctor, campos, interfaz ICronogramaVacunacionService
│   └── Funciones/
│       ├── VacunacionCronogramaService.Crud.cs        # crear/editar/listar ítems de cronograma
│       └── VacunacionCronogramaService.Filtros.cs      # resolución de granjas visibles (IFarmService) + filtros
├── VacunacionRegistroService.cs              # ancla + interfaz IVacunacionRegistroService
│   └── Funciones/
│       └── VacunacionRegistroService.Registrar.cs      # confirmar aplicado/no aplicado, cálculo de estado/desviación
└── VacunacionReportesService.cs              # llama a las funciones SQL, arma DTOs de cumplimiento

Application/Calculos/VacunacionCalculos.cs    # PURO: cálculo de franja (fecha inicio/fin dado unidad+valor+fechaEncaset),
                                               #   cálculo de estado+desviación dado fecha aplicación vs franja
Application/DTOs/Vacunacion/                  # requests/responses (CronogramaItemDto, RegistroAplicacionDto, CumplimientoDto...)

API/Controllers/
├── VacunacionCronogramaController.cs         # policy vacunacion.cronograma.administrar / .ver
├── VacunacionRegistroController.cs           # policy vacunacion.registro.aplicar
└── VacunacionReportesController.cs           # policy vacunacion.reportes.ver
```

- Namespace plano `ZooSanMarino.Infrastructure.Services` para los archivos en `Funciones/`, igual que el resto del repo.
- `VacunacionCalculos` es xUnit-testeable sin EF: dado `(FechaEncaset, UnidadObjetivo, ValorObjetivo, FechaObjetivo, RangoDiasAntes, RangoDiasDespues)` devuelve `(FechaInicioFranja, FechaFinFranja)`; dado `(franja, fechaAplicacion, umbralDias)` devuelve `(Estado, DiasDesviacion, Incumplido, RequiereMotivo)`.
- El registro de aplicación **no permite** que el front envíe `FechaAplicacion` — el handler la fija server-side.

### Permisos (migración de solo datos, sin rol nuevo)
Nueva migración `AddPermisosVacunacion` con `migrationBuilder.Sql("INSERT INTO public.permissions (...) SELECT ... WHERE NOT EXISTS (...)")` para:
`vacunacion.cronograma.ver`, `vacunacion.cronograma.administrar`, `vacunacion.registro.aplicar`, `vacunacion.reportes.ver`.
Más `backend/sql/add_vacunacion_menu.sql` (dato, patrón de `add_movimiento_pollo_engorde_menu.sql`) para el ítem de menú y su `role_menus` heredado del menú "hermano" más cercano.

### Funciones SQL (`/backend/sql/`, plantilla `fn_informe_semanal_pollo_engorde.sql` / `fn_indicadores_pollo_engorde.sql`)
- `fn_vacunacion_cumplimiento_lote.sql` — una fila por lote con % a tiempo / % tardío (1 semana / 2+ semanas) / % no aplicado / promedio días de atraso, filtros `p_company_id, p_pais_id, p_granja_ids[], p_nucleo_id, p_galpon_id, p_lote_ids[], p_linea_productiva, p_fecha_desde, p_fecha_hasta`.
- `fn_vacunacion_cumplimiento_comparativo.sql` — mismo cálculo pero pensado para comparar N lotes lado a lado (misma granja u otras), reusando `fn_vacunacion_cumplimiento_lote` como base vía `CROSS JOIN LATERAL` (patrón de `vw_liquidacion_ecuador_pollo_engorde.sql`).
- El umbral de incumplimiento se lee de `vacunacion_configuracion` dentro de la función (join por `company_id`/`pais_id`, default 14 si no hay fila).

## Frontend — estructura (Angular 22 standalone, patrón `movimientos-pollo-engorde`)

```
frontend/src/app/features/vacunacion/
├── models/
│   ├── cronograma-item.model.ts
│   ├── registro-aplicacion.model.ts
│   └── cumplimiento.model.ts
├── funciones/                                # puras, sin `this`/DI
│   ├── README.md
│   ├── mapear-cronograma-a-filas.funcion.ts   # agrupa ítems por lote, ordena por franja
│   ├── calcular-estado-visual.funcion.ts      # color/badge según Estado + Incumplido (solo presentación; el cálculo real viene del back)
│   ├── exportar-cronograma-excel.funcion.ts
│   ├── exportar-historial-excel.funcion.ts
│   └── exportar-cumplimiento-excel.funcion.ts
├── components/
│   ├── tabla-cronograma/                      # vista limpia del cronograma completo del lote
│   ├── formulario-item-cronograma/            # alta/edición de un ítem (admin)
│   ├── modal-registro-aplicacion/             # aplicar / no aplicar con motivo obligatorio condicional
│   └── graficas-cumplimiento/                 # ng2-charts (ya usado en el repo)
├── pages/
│   ├── cronograma-administracion.page.ts      # perfil administrador
│   ├── registro-aplicacion.page.ts            # perfil operador
│   └── reportes-cumplimiento.page.ts
├── services/
│   ├── vacunacion-cronograma.service.ts
│   ├── vacunacion-registro.service.ts
│   └── vacunacion-reportes.service.ts
└── vacunacion.routes.ts                        # standalone routes + guard de permisos existente
```

- Export a Excel exclusivamente vía `shared/utils/excel/exportar-tabla-excel.funcion.ts` (`exportarTablaExcel`/`exportarMultiHojaExcel`), nunca `XLSX.book_new` inline.
- Confirmaciones (ej. "marcar no aplicado") vía `ConfirmDialogService`; toasts vía `ToastService`. Prohibido `alert()`/`confirm()`.
- Filtro de granjas visibles: el componente pide al backend (que ya resuelve con `IFarmService`), no se reimplementa el filtro en el front.
- Selector de vacunas: combo que consume el endpoint de `ItemInventario` filtrado por `tipoItem=vacuna` (reusar servicio existente de inventario si ya expone ese filtro por query param, o agregarle el parámetro si falta).

## Roles/permisos y visibilidad
- Nuevos permisos `vacunacion.*` (arriba), asignables a roles existentes vía UI de Roles ya construida — no se toca código de roles.
- Visibilidad de granja: mismo mecanismo `UserFarm` + `IFarmService.GetAssignedFarmsForCompanyAsync`, filtrado siempre por `CompanyId` activo (`ICurrentUser`).

## Riesgos / verificaciones a hacer al empezar la implementación (Fase 0, antes de escribir migraciones)
1. Confirmar contra la BD real (local, `sanmarinoapplocal:5433`) qué tan poblado está `LoteId` en `lote_postura_levante` / `lote_postura_produccion` / `lote_ave_engorde` — si está bien poblado en la práctica, se puede simplificar el ancla a un solo `LoteId` (legacy) en vez de 3 FKs nullable + discriminador. Si no, el diseño de 3 FKs + `LineaProductiva` (ya en el plan) queda como definitivo.
2. Confirmar el nombre exacto del endpoint/servicio de `ItemInventario` que soporta filtrar por `tipoItem` desde el front (o si hay que agregar el query param).
3. Confirmar si existe ya una tabla de configuración por empresa reusable para `DiasUmbralIncumplido`, o si conviene la tabla dedicada `vacunacion_configuracion` propuesta.

## Plan de pruebas
- **Backend (xUnit, `tests/ZooSanMarino.Application.Tests/VacunacionCalculosTests.cs`)**: casos de franja (semana con `RangoDiasAntes/Despues`, día puntual en engorde, fecha fija); casos de estado (a tiempo, tardío 1 semana, tardío 2+ semanas → incumplido rojo, adelantado, no aplicado); umbral configurable distinto de default.
- **Backend (integración)**: crear cronograma para un lote de cada línea (Levante/Producción/Engorde), registrar aplicación a tiempo/tarde/no aplicada validando que exige motivo cuando corresponde, validar que `FechaAplicacion` no es aceptada desde el request.
- **Frontend (Karma, `frontend/src/tests/`)**: funciones puras de `funciones/` (mapeo, export), guard de visibilidad por granja en el listado.

## Siguientes pasos (fuera de este plan, STEP 2 del workflow)
1. Reemplazar `tracker_estado.md` con el checklist granular de este plan.
2. Crear la rama pedida por el usuario: `feature/modulo-vacunacion` (a confirmar nombre) — **hecho**.
3. Ejecutar Fase 0 (verificaciones arriba) antes de la primera migración.
