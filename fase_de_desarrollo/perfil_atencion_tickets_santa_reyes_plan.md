# Santa Reyes no puede crear tickets — perfil de atención + la empresa nueva que nace muda

> **Reporte (04-sep-2026):** «Santa Reyes no deja crear el ticket para asignarlo a desarrollo».
> **Diagnóstico:** no es permiso, ni menú, ni rol. Es que la empresa **no tiene un solo resolutor
> configurado**, y un tipo de ticket sin resolutor **no se ofrece**.

---

## 1 · Causa raíz, medida

`TicketPerfilService.GetTiposPermitidosAsync` arma la lista de tipos que el formulario ofrece así:

```csharp
foreach (var tipo in tiposDelNivel)
{
    var asignables = await GetAsignablesInternalAsync(tipo, paisId, companyId, ct);
    if (asignables.Count > 0)          // ← un tipo SIN resolutor no entra en la lista
        result.Add(new TipoPermitidoDto(tipo, TipoLabel(tipo), asignables));
}
```

Y `GetAsignablesInternalAsync` filtra **por empresa** en las dos fuentes (`ticket_resolutores` por
usuario y `ticket_resolutor_rol` por rol). Medido sobre la copia de producción local:

```
ticket_resolutor_rol  → 14 filas: Sanmarino, Demo, Ecuador, Panamá.   Santa Reyes: 0
ticket_resolutores    → 11 filas: Sanmarino, Demo, Ecuador, Panamá.   Santa Reyes: 0
```

⇒ `tiposPermitidos = []` ⇒ en `ticket-create.component.ts` el `<select>` de **Tipo** queda vacío,
y como `tipo` y `asignadoGuid` son `Validators.required`, **el formulario no se puede enviar nunca**.
No hay error en pantalla: la request devuelve `200 []`. Ese es exactamente el síntoma reportado.

**Lo que YA estaba bien** (verificado, para que no se toque):

| Capa | Estado en Santa Reyes |
|---|---|
| `company_permissions` | `tickets.crear`, `tickets.gestionar`, `tickets.admin`, `tickets.indicadores` → **todos `is_enabled = true`** |
| `role_permissions` | «Santa Reyes Administrador» y «Santa Reyes Implementador» tienen los 3 permisos de tickets |
| `company_menus` / `role_menus` | `tickets` + `tickets.mis` habilitados y asignados a los dos roles |
| Nivel del solicitante | Con `tickets.gestionar`/`tickets.admin` el nivel es **IMPLEMENTADOR** ⇒ los 4 tipos (incluye DESARROLLO y REQUERIMIENTO) |

**El rol ya existe:** `Santa Reyes Implementador` (id 31, `is_company_admin = true`, usuario
`implementador@santareyes.com`) lo creó `20260725190000_SeedEmpresaSantaReyes`. No se crea otro.

## 2 · Por qué toda empresa nueva nace igual de muda

`CompanyService.CreateAsync` siembra `company_permissions` con el catálogo completo…

```csharp
await _companyPermissionService.SembrarCatalogoCompletoSiVaciaAsync(c.Id);
```

…y **nada más**. El rol **`Admin`** —el rol global de aplicación, el del equipo de desarrollo— está
configurado como resolutor de `DESARROLLO` en las otras cuatro empresas *una fila por empresa*
(`ticket_resolutor_rol` incluye `company_id` en su índice único a propósito). Como la creación de la
empresa no replica esa fila, **la empresa nace sin ninguna vía para escalar a desarrollo** y nadie se
entera hasta que alguien intenta abrir un caso. Es la queja textual del reporte.

## 3 · Enfoque

Dos entregables independientes, uno de datos y uno de código:

**(A) Migración data-only para Santa Reyes** — configura su perfil de atención, decidido con el
usuario:

| Rol | Tipos que atiende | Quién queda como asignable |
|---|---|---|
| `Admin` (global, desarrollo) | `DESARROLLO`, `REQUERIMIENTO` | Jose Moises Desarrollo |
| `Santa Reyes Implementador` | `SOPORTE`, `DUDAS`, `DESARROLLO`, `REQUERIMIENTO` | Implementador Santa Reyes |

Todas con `pais_id = NULL` (global, igual que las 14 filas existentes) y `company_id` = Santa Reyes.
Resultado: los **4 tipos** quedan disponibles en el formulario, y Desarrollo/Requerimiento ofrecen
**dos** destinos (el equipo global o el implementador de la empresa).

**(B) Arreglo estructural** — que la empresa nueva nazca con el resolutor global de tickets, igual
que hoy nace con el catálogo de permisos. La **decisión** (qué rol es el global y qué tipos cubre) va
en `Application/Calculos/` como lógica pura con tests; el service sólo la aplica.

## 4 · Archivos

**Backend — nuevos**

| Archivo | Qué |
|---|---|
| `Infrastructure/Migrations/20260904170000_SeedPerfilAtencionTicketsSantaReyes.cs` (+ `.Designer.cs`) | Siembra las 6 filas de `ticket_resolutor_rol`. Idempotente. |
| `Application/Calculos/TicketPerfilAtencionSiembraCalculos.cs` | Puro: qué rol es el resolutor global y qué filas le faltan a una empresa. |
| `tests/ZooSanMarino.Application.Tests/TicketPerfilAtencionSiembraCalculosTests.cs` | xUnit del cálculo. |
| `Infrastructure/Services/CompanyService/Funciones/CompanyService.PerfilAtencionTickets.cs` | `partial class`: aplica el cálculo al crear empresa. |

**Backend — modificados**

| Archivo | Cambio |
|---|---|
| `Infrastructure/Services/CompanyService/Funciones/CompanyService.Crud.cs` | Una línea en `CreateAsync`: `await SembrarResolutorGlobalTicketsAsync(c.Id);` |

**Frontend:** ninguno. El formulario ya funciona; le faltaban los datos.

## 5 · Reglas de negocio

1. **Un tipo sin resolutor no existe para el solicitante.** Es la regla que ya estaba y no se toca:
   se corrige el dato, no el filtro. Bajar el `if (asignables.Count > 0)` mostraría tipos que al
   enviar rebotan con *«El resolutor seleccionado no está disponible»* — peor que ocultarlos.
2. **El rol resolutor global se identifica por nombre EXACTO** (`admin` / `administrador`,
   ignorando mayúsculas y espacios al borde), nunca por substring. En la base conviven
   `Admin Panama`, `Admin Demo`, `Ecuador Administrador`, `Santa Reyes Administrador` y
   `ADMINISTRADOR DE GRANJA`: son administradores **de su empresa**, no el equipo de desarrollo.
   Misma frontera que `CatalogoGlobalAutorizacionCalculos`, y por el mismo motivo.
3. **`pais_id = NULL` = global.** Es lo que usan las 5 filas de `Admin` existentes; el filtro del
   service es `(r.PaisId == null || r.PaisId == paisId)`.
4. **La idempotencia va por `NOT EXISTS`, no por el índice único.** `ux_ticket_resolutor_rol_role_
   tipo_pais_company` incluye `pais_id`, y en Postgres **dos NULL no chocan**: sin el `NOT EXISTS`
   (con `pais_id IS NULL` explícito) una segunda corrida duplicaría las 6 filas sin error.
5. **Fila apagada = fila ausente.** El service exige `r.Activo`, así que además del `INSERT` va un
   `UPDATE ... SET activo = true WHERE activo IS DISTINCT FROM true`.
6. **Localizar por `companies.identifier` y `roles.name`**, jamás por id: difieren local↔prod.
7. **La empresa nueva se siembra sólo si está vacía** (mismo criterio que
   `SembrarCatalogoCompletoSiVaciaAsync`): no se pisa una configuración hecha a mano.
8. **Sin DDL.** No hay cambio de schema; sólo filas.

## 6 · Casos de prueba

**Cálculo puro (`TicketPerfilAtencionSiembraCalculosTests`)**

| # | Caso | Esperado |
|---|---|---|
| 1 | Roles `Admin` + `Admin Panama` + `Santa Reyes Administrador`, empresa sin filas | 4 filas, **todas del rol `Admin`** |
| 2 | Sin ningún rol global (`Admin Panama`, `Ecuador Administrador`) | 0 filas — fail-closed, no inventa resolutor |
| 3 | `administrador` / `ADMIN` / `  Admin  ` | reconocidos (exacto, case-insensitive, con trim) |
| 4 | La empresa ya tiene `(Admin, DESARROLLO)` | sólo las 3 que faltan; no duplica |
| 5 | La empresa ya tiene los 4 tipos | 0 filas |
| 6 | `roles` null / vacío / nombres en blanco / `null` | 0 filas, sin excepción |
| 7 | Dos roles empatan el nombre global (`Admin` y `administrador`) | filas para ambos, sin duplicar el par (rol, tipo) |
| 8 | Los tipos emitidos son exactamente los 4 de `TicketTipos` | orden determinista |

**Verificación en base (transacción revertida, contra la copia de producción)**

| # | Consulta | Antes | Después |
|---|---|---|---|
| V1 | Filas de `ticket_resolutor_rol` de Santa Reyes | 0 | 6 activas |
| V2 | Filas de las otras 4 empresas | 14 | **14, idénticas** — invariante multi-empresa |
| V3 | Correr el `Up` dos veces | — | sigue en 6 (idempotente) |
| V4 | Asignables de `DESARROLLO` para el usuario `implementador@santareyes.com` | vacío | Jose Moises Desarrollo + Implementador Santa Reyes |
| V5 | `Down` | — | vuelve a 0 sin tocar a las otras empresas |

**Build y tests:** `dotnet build` 0 errores + `dotnet test` verde (incluye los 8 casos nuevos).
Front: no se toca, no hay `yarn build` que correr.

## 7 · Lo que NO se hace (y por qué)

- **No se crea un rol nuevo «Implementador»**: `Santa Reyes Implementador` ya existe en producción
  desde el alta de la empresa. Dos roles con el mismo propósito confunden al asignar usuarios.
- **No se habilita `italjira.configuracion`** en Santa Reyes. Es la pantalla desde la que un admin
  configuraría esto solo, pero abre además Backlog/Tablero/Roadmap del área de desarrollo. Si se
  quiere self-service, es una decisión aparte.
- **No se toca `TicketService.CreateAsync`**, cuya validación por rol *no* filtra por empresa a
  propósito (comentario explícito en el código). Espejarlo acá sería cambiar comportamiento.
- **No se backfillea el resto de empresas**: las cuatro ya tienen su fila de `Admin → DESARROLLO`.
