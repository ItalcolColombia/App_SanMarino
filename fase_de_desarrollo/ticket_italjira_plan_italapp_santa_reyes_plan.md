# Registrar en ItalJira el Plan de Trabajo de Italapp para Santa Reyes

> **Pedido (19-ago-2026).** Crear una migración que siembre en ItalJira el **caso** con el plan de
> desarrollo ya planteado para Santa Reyes —con sus horas, sus tareas y sus subtareas, completo y
> con el detalle de lo que hay que hacer—, **creado por el administrador** pero **solicitado por el
> usuario Lenin de la empresa Santa Reyes**.
>
> **Fuente de los datos:** `~/Desktop/Plan_de_Trabajo_Santa_Reyes.xlsx` (v2.0, hojas *Plan diario*,
> *Cronograma*, *Carga por jornada*, *Alcance* y *Guías genéticas*), el mismo entregable comercial
> que declara `tracker_estado.md` V30.2. Contexto técnico:
> [`santa_reyes_requerimientos_italapp_plan.md`](santa_reyes_requerimientos_italapp_plan.md).
>
> **No se toca `tracker_estado.md`** por pedido explícito del usuario (hay otras sesiones
> trabajando el mismo repo). El bloque **V30** sigue siendo el dueño del estado de este trabajo.

---

## 1. Enfoque

Migración **data-only** (Designer clonado, `ModelSnapshot` intacto), sin DDL: siembra filas en
`historias`, `tickets`, `ticket_tareas` y `ticket_notas`. No hay código C# de aplicación, no hay
entidades nuevas y no hay contrato de API que cambie — el módulo de tickets ya soporta todo lo que
se siembra (`Historia`, `Ticket.SolicitanteUserGuid`, `TicketTarea.ParentTareaId`).

### 1.1 Jerarquía sembrada

```
Historia  HIS-2026-NNNN   Implementacion de Italapp para Santa Reyes            100 h
└── Caso  TK-2026-NNNNNN  (tipo REQUERIMIENTO · solicitante Lenin · empresa SR) 100 h
    ├── Tarea  -T1   F0 · Parametrizacion por empresa                             6 h
    │   ├── Subtarea -T1-S1  F0.1  Banderas de comportamiento en la ficha         3 h
    │   └── Subtarea -T1-S2  F0.2  Catalogo de items de huevo y alimento          3 h
    ├── Tarea  -T2   F1 · Estructura fisica de granja y ERP                      10 h
    │   └── … (2 subtareas)
    …
    └── Tarea  -T13  F12 · Despliegue                                             2 h
        └── Subtarea -T13-S1  F12.1 Despliegue a produccion y verificacion        2 h
```

**13 tareas** (un paquete de trabajo cada una) + **29 subtareas** (una actividad cada una) = 42
filas en `ticket_tareas`. Las horas de cada tarea son la **suma exacta** de las de sus subtareas y
el total da **100 h**, igual que el Excel.

> ℹ️ La portada del Excel y el bloque V30 del tracker dicen «12 paquetes». La hoja *Cronograma*
> tiene **13 códigos de paquete distintos** (F0…F12): F0 se contó aparte por ser transversal.
> El seed usa los 13 que trae el dato; el conteo de actividades (29) y el de horas (100) coinciden.

### 1.2 Identidad — por dato, nunca por guid fijo

| Rol | Cómo se resuelve | Si no aparece |
|---|---|---|
| **Administrador** (creador y responsable) | por email `moiesbbuga@gmail.com` sobre `users ▸ user_logins ▸ logins` | `RAISE NOTICE` + `RETURN`: no se siembra nada y la app arranca igual |
| **Lenin** (solicitante delegado) | usuario de la empresa **Santa Reyes** con `first_name`/`sur_name`/email que contenga `lenin`; si no está en SR, se busca en cualquier empresa | el caso se siembra igual, **sin** solicitante delegado y con `RAISE NOTICE` — la asignación se hace después en un clic desde la pantalla |
| **Empresa del caso** | la de Lenin (`user_companies`), prefiriendo Santa Reyes; si Lenin no existe, `companies.name = 'Santa Reyes'` | último recurso: la empresa del último ticket del administrador |
| **País** | `user_companies.pais_id` de la empresa resuelta | 1 (Colombia) |

Los ids **difieren entre local y producción**: por eso nada se referencia por id ni por guid literal.

### 1.3 Por qué la empresa del caso es Santa Reyes y no la del administrador

`TicketService.CreateAsync` mueve el caso a la empresa del **solicitante** cuando se delega, porque
`SearchMisTicketsAsync` filtra por la empresa efectiva de quien mira: si el caso quedara en la
empresa del administrador, **Lenin nunca lo vería** en «Mis solicitudes» aunque recibiera el correo.
El seed replica esa regla a mano (memoria `tickets-jira-casos-tareas`).

Las **tareas** no se filtran por empresa: su visibilidad se deriva del caso
(`TicketTareaService.PuedeVerAsync` acepta al solicitante). `company_id` en `ticket_tareas` es solo
auditoría; se siembra igual al del caso para que sea coherente.

### 1.4 Registro (qué se le puede mostrar al cliente)

El solicitante **ve el caso y sus tareas**. Por eso los textos sembrados son los del **entregable
comercial** (hojas *Alcance* y *Plan diario* del Excel), que presentan todo el alcance como trabajo
por ejecutar. La §2 de `santa_reyes_requerimientos_italapp_plan.md` —la auditoría de qué base ya
existe en el repo— **no se copia a la base de datos**: es interna, y V30.4 la declara como
no-exponible. El detalle técnico que sí va en las descripciones es de nivel funcional y de
aceptación (qué queda hecho, dónde se ve, qué no se debe romper).

### 1.5 Estados

- **Caso → `ABIERTO`.** Es el estado con el que nace un caso registrado a nombre de otro
  (`TicketEstados.Abierto` en `CreateAsync`) y es lo que corresponde: V30.5 (aprobación del
  cliente) sigue sin darse y el bloque V30 declara la ejecución «sin arrancar». Ningún trabajo se
  da por iniciado.
- **Historia y las 42 tareas → `BACKLOG`.** Nada empezó.
- `fecha_primera_apertura` queda **NULL**: ningún resolutor lo abrió todavía.

### 1.6 Planificación

`fecha_inicio_plan` / `fecha_fin_plan` salen de la hoja *Cronograma*: el caso y la historia van de
**2026-08-19 a 2026-09-01**; cada tarea abarca de su primer a su último día; cada subtarea empieza
y termina el día en que está agendada. `fecha_limite` del caso = 2026-09-01 (el compromiso de
entrega, base del semáforo de SLA).

`orden` se siembra **0..41 dentro del caso**, contiguo y sin huecos: el tablero de tareas está
scopeado por `ticket_id` (`TicketTareaService`), y un `orden` con huecos o repetido deja las
tarjetas barajadas en la próxima carga.

---

## 2. Archivos

| Archivo | Qué es |
|---|---|
| `backend/src/ZooSanMarino.Infrastructure/Migrations/20260819120000_SeedTicketPlanItalappSantaReyes.cs` | Migración: documentación + `Up`/`Down` |
| `…/20260819120000_SeedTicketPlanItalappSantaReyes.Seed.cs` | `partial` con el SQL del seed (por tamaño) |
| `…/20260819120000_SeedTicketPlanItalappSantaReyes.Designer.cs` | Designer **clonado** de `20260819001837_AddSesionesActivas` (solo cambian el `[Migration]` y el nombre de la clase) |
| `fase_de_desarrollo/ticket_italjira_plan_italapp_santa_reyes_plan.md` | Este plan |

**No se modifica:** `ZooSanMarinoContextModelSnapshot.cs` (no hay cambio de modelo),
`tracker_estado.md` (pedido explícito), ni ningún archivo compartido con otras sesiones.

Timestamp `20260819120000`: posterior a `20260819001837_AddSesionesActivas`, que es la última del
árbol, y muy posterior a `20260725190000_SeedEmpresaSantaReyes` — que es el que crea la empresa que
este seed busca por nombre (regla de CLAUDE.md §🏢.7).

---

## 3. Reglas de negocio

1. **Idempotencia.** Historia y caso se buscan por `titulo` (+ `deleted_at IS NULL`); las tareas y
   subtareas por `codigo` (`HIS-2026-NNNN-Tn` y `HIS-2026-NNNN-Tn-Sm`, derivados del id real de la
   historia). La nota de solicitante, por su texto. Correrla dos veces no cambia una sola fila la
   segunda vez.
2. **Fail-open.** Sin el administrador no se siembra nada. Sin Lenin se siembra el caso sin
   delegación. Un seed no puede tumbar el arranque de la app (`Database__RunMigrations=true`).
3. **El código de la historia se completa antes de las tareas.** Si la historia existiera con
   `codigo` NULL, el `WHERE NOT EXISTS` compararía contra NULL, no encontraría nada y reinsertaría
   las 42 tareas en cada corrida.
4. **Las subtareas resuelven su padre por `codigo`**, no por id: el `parent_tarea_id` se obtiene con
   un `JOIN` contra la tarea del paquete recién insertada.
5. **Sin correo.** Es SQL: no pasa por el servicio de notificación. `notificado_correo` queda en
   `false` — nadie recibe nada por este deploy.
6. **`Down` simétrico.** Borra subtareas → tareas → nota → caso → historia, todo localizado por el
   mismo `codigo`/`titulo`. No toca nada más.

---

## 4. Casos de prueba

| # | Escenario | Resultado esperado |
|---|---|---|
| 1 | `dotnet ef database update` en local | Migración aplicada sin error; `dotnet ef migrations list` sin pendientes |
| 2 | Conteo tras aplicar | 1 historia · 1 caso · 13 tareas · 29 subtareas |
| 3 | Suma de horas | `Σ subtareas = Σ tareas = caso = historia = 100.00` |
| 4 | Cuadre por paquete | Cada tarea = suma exacta de sus subtareas (6/10/10/10/8/9/3/17/7/5/5/8/2) |
| 5 | Cuadre por jornada | Los 10 días del plan suman **10 h cada uno** |
| 6 | Jerarquía | Las 29 subtareas tienen `parent_tarea_id` no nulo y apuntan a una tarea del mismo caso; ninguna tarea de paquete tiene padre |
| 7 | Solicitante | Con Lenin en la BD: `solicitante_user_guid` = Lenin, `company_id` = Santa Reyes y la nota `SISTEMA_SOLICITANTE` existe. Sin Lenin: caso sembrado, `solicitante_user_guid` NULL, sin nota, `RAISE NOTICE` |
| 8 | Idempotencia | Segunda corrida del `Up` ⇒ 0 filas afectadas y los conteos no se mueven |
| 9 | `Down` | Deja los conteos en 0 y no borra ninguna otra historia, caso ni tarea |
| 10 | `orden` | 0..41 contiguo, sin repetidos, dentro del caso |
| 11 | No regresión | `dotnet build` 0 errores y `dotnet test` verde: el seed no toca cálculo ni servicio, así que ninguna suite debería moverse |

**Fuera de alcance del gate multipaís:** no se toca `fn_seguimiento_diario_engorde`,
`fn_cuadre_alimento_engorde` ni ningún `*SaldoAlimento*` — el seed solo escribe en las cuatro tablas
del módulo de tickets.
