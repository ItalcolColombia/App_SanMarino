# LEVANTE-HASTA-SEMANA — límite de semanas del seguimiento diario de levante, por empresa (02-oct-2026)

## Pedido
En Configuración → Empresas, un parámetro «el levante se registra hasta la semana N». El seguimiento
diario de levante solo admite registros hasta el **último día** de esa semana de vida del lote; desde
la semana N+1 se rechaza, para obligar a **cerrar el lote** (pasar a producción). Ej.: Sanmarino = 25.

## Enfoque (patrón «Features por EMPRESA», precedente `huevos_levante_desde_semana`)
- **BD:** `companies.levante_hasta_semana integer NULL`. `NULL` = sin límite = comportamiento de hoy,
  byte a byte, para toda empresa. Nombre por comportamiento, no por tenant. Sin seed: lo configura
  el admin desde la UI (el «25 para Sanmarino» era un ejemplo).
- **Semana de vida:** la canónica `HuevosLevanteCalculos.SemanaVida` (`floor(días/7)+1`, el día del
  encaset es la semana 1). Último día permitido = `encaset + N·7 − 1`.
- **Lógica pura:** `Application/Calculos/LevanteSemanaLimiteCalculos.cs` (`PermiteRegistro`,
  `FechaLimite`, `Mensaje`) + tests xUnit. Sin fecha de encaset no hay semana evaluable → se permite
  (mismo fail-open que el gate de huevos: bloquear ahí sería un 400 sin remedio).
- **Backend:** `SeguimientoLoteLevanteService` resuelve el límite **por datos** (`farms.company_id`
  de la granja del lote) y valida en `CreateAsync` y `UpdateAsync` antes de tocar inventario. En la
  edición solo se valida si la fecha CAMBIA (permite corregir filas viejas que ya pasaban el límite
  antes de configurarlo; impide mover un registro fuera de la ventana). Cubre la pantalla y la app
  móvil (`SyncPushService` usa el mismo service). Traslados/arrastre/migraciones masivas NO se tocan
  (son movimientos del sistema, no seguimiento diario).
- **Sentinel de borrado `0`** al editar (reusa `ParametroEmpresaOpcionalCalculos` /
  `resolverSemanaOpcionalParaGuardar`).
- **DTOs/proyecciones:** `CompanyDto`, `CreateCompanyDto`, `UpdateCompanyDto`, `CompanyService.ToDto`,
  `CompanyService.Crud`, `CompanyResolver` (2), `CompanyPaisService`.
- **Front:** campo en Configuración → Empresas (bloque «Huevos en levante» → nuevo bloque «Duración
  del levante», 1–60). `ActiveCompanyConfigService` expone `levanteHastaSemana`. En el modal de
  seguimiento de levante: `max` de la fecha = min(ventana actual, último día permitido), hint con la
  semana límite y guard en `onSave` con toast que pide cerrar el lote. Función pura en
  `features/lote-levante/funciones/levante-hasta-semana.funcion.ts`.

## Archivos
Backend: `Company.cs`, `CompanyConfiguration.cs`, DTOs (3), `CompanyService.cs`, `CompanyService.Crud.cs`,
`CompanyResolver.cs`, `CompanyPaisService.cs`, `SeguimientoLoteLevanteService.cs` (+`.Crud.cs`),
`Calculos/LevanteSemanaLimiteCalculos.cs`, migración `AddLevanteHastaSemana` (idempotente, con
Designer + snapshot), test `LevanteSemanaLimiteCalculosTests.cs`.
Front: `company.service.ts`, `active-company-config.service.ts`, `company-management.component.{ts,html}`,
modal de levante `.ts/.html`, `funciones/levante-hasta-semana.funcion.ts`.

## Casos de prueba
1. Límite null → todo igual (cualquier semana pasa).
2. N=25, encaset 01-ene: registro día 174 (semana 25, último día) pasa; día 175 (semana 26) → 400 con
   mensaje que cita semana límite, semana del registro y fecha límite.
3. Sin fecha de encaset → pasa.
4. Edición sin cambiar la fecha de una fila fuera de límite → pasa; moverla a fuera de límite → 400.
5. Configuración → Empresas: guardar 25, recargar y ver 25; vaciar → vuelve a null (sentinel 0).
6. Smoke: empresa con límite OFF sin cambios visibles; con límite ON el date picker no pasa del último día.
