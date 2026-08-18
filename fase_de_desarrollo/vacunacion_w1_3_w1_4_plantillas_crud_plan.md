# Vacunación W1.3 + W1.4 — la plantilla deja de ser una tabla vacía

**Fecha:** 2026-08-17 · **Continúa:** [`vacunacion_cronograma_vivo_plantillas_plan.md`](vacunacion_cronograma_vivo_plantillas_plan.md) §4 (fase W1)
**Antecedente:** W1.1 (tablas + migración) y W1.2 (`VacunacionPlantillaCalculos.ResolverEfectiva`, 28 tests)
entregados en `a19807b`. Hoy las dos tablas existen, la regla de resolución está escrita y probada,
y **nadie puede cargar una plantilla**: no hay endpoint ni pantalla.

---

## 0. Alcance

| Pieza | Entra | No entra |
|---|---|---|
| CRUD de plantillas y de sus ítems | ✅ W1.3 | — |
| Permisos + menú | ✅ W1.3 | — |
| Pantalla de administración | ✅ W1.4 | — |
| Vista previa «¿qué plantilla le toca a este lote?» | ✅ W1.3 (solo lectura) | materializar |
| Materializador al cronograma del lote | ❌ | **W2** |
| Bandeja de pendientes / scoping fino | ❌ | **W3 / W4** |

**Regla que gobierna todo el bloque:** una empresa **sin plantillas se comporta byte a byte como hoy**.
Nada de lo que entra acá escribe una sola fila en `vacunacion_cronograma_items` — la materialización
es W2, y separarlas es lo que hace que este bloque sea de riesgo bajo.

---

## 1. Enfoque arquitectónico

- **Servicio nuevo, no un partial del cronograma.** `VacunacionCronogramaService` escribe el
  cronograma **del lote**; la plantilla es el plan **de la empresa**. Mezclarlos dejaría un servicio
  con dos sujetos y, sobre todo, dos permisos distintos en la misma clase.
- **Partición por responsabilidad** (CLAUDE.md §🧩): ancla con campos/ctor/validaciones + `Funciones/`
  con `Crud` y `Efectiva`. Namespace plano `ZooSanMarino.Infrastructure.Services`.
- **Lógica pura arriba**: lo que se puede decidir sin EF va a `Application/Calculos/VacunacionPlantillaCalculos.cs`
  (ya existe) con sus tests. El servicio resuelve datos y delega.
- **Empresa por `_currentUser.CompanyId`, fail-closed**: toda consulta filtra empresa **y**
  `deleted_at IS NULL`. Un id de otra empresa devuelve *no encontrado*, nunca datos.

## 2. Archivos

**Backend**
| Archivo | Qué |
|---|---|
| `Application/DTOs/Vacunacion/VacunacionPlantillaDtos.cs` | 7 records (lista, detalle, ítem, 4 requests, efectiva) |
| `Application/Interfaces/IVacunacionPlantillaService.cs` | contrato |
| `Application/Calculos/VacunacionPlantillaCalculos.cs` | **+3 funciones puras** (ver §3) |
| `Infrastructure/Services/Vacunacion/VacunacionPlantillaService.cs` | ancla |
| `Infrastructure/Services/Vacunacion/Funciones/VacunacionPlantillaService.Crud.cs` | alta/edición/baja de plantilla e ítems |
| `Infrastructure/Services/Vacunacion/Funciones/VacunacionPlantillaService.Efectiva.cs` | «¿cuál le toca a este lote?» (lectura) |
| `API/Controllers/VacunacionPlantillaController.cs` | 10 endpoints |
| `API/Program.cs` | DI |
| Migración `…_AddPermisosYMenuVacunacionPlantillas` | 2 permisos + `role_permissions` heredados + menú |

**Frontend**
| Archivo | Qué |
|---|---|
| `features/vacunacion/models/vacunacion-plantilla.model.ts` | tipos 1:1 con los DTOs |
| `features/vacunacion/services/vacunacion.service.ts` | + métodos de plantillas |
| `features/vacunacion/funciones/construir-filas-plantilla.funcion.ts` | PURA: filas y etiqueta de objetivo |
| `features/vacunacion/funciones/describir-plantilla.funcion.ts` | PURA: alcance legible + estado de vigencia |
| `features/vacunacion/funciones/exportar-plantillas-excel.funcion.ts` | helper compartido de Excel |
| `features/vacunacion/pages/plantillas/` | página (maestro-detalle) |
| `features/vacunacion/components/modal-plantilla/` | cabecera de la plantilla |
| `features/vacunacion/components/modal-item-plantilla/` | vacuna del plan |
| `features/vacunacion/vacunacion-routing.module.ts` | ruta `plantillas` |

## 3. Reglas de negocio (contrato de los tests)

1. **Ítem sin `Fecha`.** Ya lo fija el CHECK y `MotivoItemInvalido`; el CRUD lo rechaza con mensaje.
2. **Una plantilla por `(empresa, línea, raza, vigente_desde)`.** Duplicarla no rompe la resolución
   (`ResolverEfectiva` es total: gana el id mayor) pero **sí rompe al humano**: dos filas idénticas en
   pantalla y ninguna pista de cuál manda. Se rechaza al guardar, nombrando la que ya existe.
   → `MotivoPlantillaDuplicada` (pura).
3. **Ítem repetido dentro de la plantilla** = misma vacuna en el mismo objetivo. Se rechaza: no es un
   refuerzo (un refuerzo va en otra semana), es una carga doble.
   → `MotivoItemDuplicado` (pura).
4. **La línea manda la unidad**: Postura ⇒ `Semana`, Engorde ⇒ `Dia` (`UnidadPorDefecto`, ya existe).
   Guardar un ítem con la unidad que no corresponde a la línea de su plantilla se rechaza.
   → `MotivoUnidadNoCorrespondeALinea` (pura).
5. **Borrar es soft-delete con sello compartido** (patrón V9.3): la plantilla y sus ítems reciben el
   **mismo `deleted_at`**, para poder reconocer después qué se borró junto con qué.
6. **La vacuna se valida contra el catálogo de la empresa activa** (igual que el cronograma).
7. **`efectiva` no escribe nada.** Responde qué plantilla le tocaría al lote y **por qué** (o por qué
   ninguna). Es la vista previa que hace auditable a W2 antes de que W2 exista.
8. Sin plantillas ⇒ el módulo se comporta como hoy. Ningún camino existente se toca.

## 4. Permisos y menú

| Clave | Quién la recibe en la migración | Por qué |
|---|---|---|
| `vacunacion.plantillas.ver` | los roles que ya tienen `vacunacion.cronograma.ver` | quien puede leer el plan de un lote puede leer el de la empresa |
| `vacunacion.plantillas.administrar` | los roles que ya tienen `vacunacion.cronograma.administrar` | mismo perfil administrador |

**Claves nuevas, no reutilizadas.** Heredarlas de las de cronograma deja *hoy* exactamente la misma
población que ya podía editar cronogramas (cero cambio efectivo de acceso), pero mañana permite
quitar «editar el plan de toda la empresa» sin quitar «editar el cronograma de un lote» — que es una
distinción real: el plan de empresa afecta a todos los lotes futuros.

Menú `vacunacion.plantillas` → `/vacunacion/plantillas`, hijo del grupo `vacunacion` existente,
idempotente por `key` (patrón de `20260714193209_AddVacunacionMenu`). Sin `role_menus` automático:
se asigna por la UI de Roles, como el resto del módulo.

## 5. Casos de prueba

**xUnit (puros, `VacunacionPlantillaCalculosTests`)**
- `MotivoPlantillaDuplicada`: misma tupla ⇒ mensaje; distinta raza / distinta vigencia / otra empresa ⇒ `null`;
  la **propia** plantilla al editarse no cuenta como duplicada de sí misma; raza `null` vs `''` vs `'  '` = el mismo comodín.
- `MotivoItemDuplicado`: misma vacuna+unidad+valor ⇒ mensaje; misma vacuna en otra semana ⇒ `null`;
  al editar, el ítem no choca consigo mismo.
- `MotivoUnidadNoCorrespondeALinea`: Engorde+Semana ⇒ mensaje; Engorde+Dia ⇒ `null`; Levante/Produccion+Semana ⇒ `null`;
  Levante+Dia ⇒ `null` (se permite: un día exacto en postura es programable, lo que no se permite es lo inverso).

**Smoke HTTP** (backend propio, puerto libre al terminar)
- crear plantilla → agregar 2 ítems → listar → editar ítem → duplicar plantilla ⇒ **400 con motivo** →
  `efectiva` de un lote de la línea ⇒ devuelve la plantilla y el motivo → borrar ⇒ plantilla e ítems
  con el **mismo `deleted_at`** → listar ⇒ ya no aparece → `efectiva` ⇒ vuelve a «sin plantilla».
- Con la BD sin plantillas: el cronograma de un lote responde **idéntico** a antes del cambio.

**Smoke UI**: la pantalla lista, crea, edita y borra; los modales abren y cierran **dos veces** sin
colgarse (gate de change detection).

## 6. Validación

- `dotnet build` 0 errores · `dotnet test` verde (los 2.656 previos + los nuevos)
- `yarn build` 0 errores (único warning aceptado: bundle budget preexistente)
- `node scripts/verificar-change-detection.js` — los componentes nuevos con `changeDetection` explícito
- Sin procesos huérfanos; la BD local queda como estaba (la migración de permisos **no** se aplica
  acá: la base está atrasada respecto de `main` por migraciones de otras sesiones — se valida por
  transacción, igual que W1.1)
