# Flujos de validación parametrizables por empresa

> Estado: **plan técnico propuesto; implementación no iniciada**  
> Fecha: 23-sep-2026  
> Primera empresa objetivo: **Santa Reyes**  
> Primera integración: **Seguimiento diario de Levante** y **Seguimiento diario de Producción**  
> Tracker: [tracker_estado.md](../tracker_estado.md) — bloque `FLUJOS-VALIDACION-EMPRESA`

## 1. Resultado buscado

Crear un módulo transversal llamado **Flujos de validación** —en la interfaz puede presentarse como
“Secuencias de confirmación”— donde una empresa configure cuántas validaciones necesita cada proceso,
en qué orden se ejecutan y quién puede realizar cada una.

El primer caso de uso es Santa Reyes:

1. El operario o ponedor registra el seguimiento diario.
2. El registro queda persistido y visible, pero sus efectos quedan **separados**, no aplicados.
3. Etapa 1: valida, por ejemplo, Administración de granja.
4. Etapa 2: valida el Técnico de granja.
5. Etapa 3: valida Costos.
6. Solo la última validación aplica el consumo de alimento, las mortalidades/bajas y los demás
   descuentos por el camino transaccional que ya existe.

La configuración debe ser reutilizable por otras empresas y extensible después a ingresos de
inventario, ventas, movimientos de huevos, movimientos de aves y otros procesos que necesiten una
aprobación previa.

### Aclaración funcional importante

“No guardar hasta terminar las validaciones” no debe significar perder la captura del operario. La
captura se guarda inmediatamente como **pendiente de aprobación**; lo que se difiere hasta la última
firma son los efectos irreversibles de negocio. Este es precisamente el patrón que el repositorio ya
usa con las reservas de alimento y aves.

## 2. Diagnóstico del código actual

La idea no parte de cero. Hoy existe un subsistema de doble validación con estas piezas:

- `companies.requiere_validacion_seguimiento_diario`: flag general por empresa.
- `SeguimientoValidacionController` e `IValidacionSeguimientoService`.
- `ValidacionSeguimientoService` organizado en `Funciones/`.
- reservas persistentes `seguimiento_reserva_alimento` y `seguimiento_reserva_aves`.
- columnas `validado`, `validado_at`, `validado_por` en los seguimientos; Reproductora reutiliza
  `confirmado` porque esa marca gobierna su trigger de cruce.
- finalización atómica: al validar se toma la fila con un `UPDATE ... WHERE validado = false`, se
  aplican alimento y aves en una transacción y se marcan las reservas como `APLICADA`.
- permisos actuales por módulo: `seguimiento_levante.validar`,
  `seguimiento_produccion.validar`, `seguimiento_engorde.validar` y el permiso de Reproductora.
- UI y endpoint de validación individual y en bloque.

### Brecha real

El modelo actual sabe responder únicamente: “¿este usuario tiene el permiso de validar?”. No sabe:

- cuál es la etapa actual;
- cuántas firmas faltan;
- qué rol o usuario firma cada etapa;
- si dos etapas deben ser firmadas por personas diferentes;
- qué versión del flujo estaba vigente cuando se creó el registro;
- quién aprobó o rechazó cada etapa;
- qué ocurre cuando el flujo cambia mientras hay registros pendientes.

Por eso no conviene agregar tres columnas de “validador 1/2/3” al seguimiento ni crear más permisos.
Eso acoplaría el caso Santa Reyes a dos tablas y obligaría a repetir el mismo diseño en cada módulo.

## 3. Decisión de arquitectura

Se implementará un **motor genérico de flujos secuenciales**, con dos capas claramente separadas:

1. **Motor de flujo**: empresa, definición versionada, etapas, asignados, instancia, firmas,
   devoluciones escalonadas, novedades persistentes, auditoría y concurrencia. No conoce inventario,
   huevos ni aves.
2. **Adaptador del proceso**: conecta un tipo de registro con el motor y sabe cómo resolver la empresa,
   resumir el registro, separar/liberar sus efectos y ejecutar la finalización real.

En la Fase 1 se registran dos adaptadores:

- `SEGUIMIENTO_LEVANTE`
- `SEGUIMIENTO_PRODUCCION`

Agregar Inventario más adelante no requerirá rediseñar el motor. Requerirá registrar, por ejemplo,
`INGRESO_INVENTARIO` y construir su adaptador transaccional.

### Lo dinámico y lo que necesariamente sigue siendo código

La **cantidad, el orden y los responsables** son datos configurables. La forma de aplicar el efecto
final no puede inventarse desde una pantalla: cada proceso necesita un adaptador probado. El módulo
solo ofrecerá procesos presentes en el catálogo global de procesos implementados. Así se evita que un
administrador cree un flujo “Ventas” cuando el backend todavía descontaría la venta al guardar.

`permission_modules` no se reutiliza como catálogo de procesos. Esa tabla agrupa permisos y los
materializa en `company_permissions`; no representa operaciones de negocio ni contiene un finalizador.

## 4. Reglas funcionales propuestas

Estas son las reglas recomendadas para cerrar antes de implementar:

1. **Secuencia estricta.** Solo la etapa actual se puede aprobar. La etapa 2 no aparece habilitada
   mientras la 1 siga pendiente.
2. **Uno o varios candidatos por etapa.** Una etapa puede asignarse a varios roles y/o usuarios.
   En Fase 1 significará “cualquiera de ellos puede dar la firma requerida” (`ANY`, una firma).
3. **Un rol se evalúa dentro de la empresa de la instancia.** No basta con tener el mismo rol en otra
   empresa. Debe existir `user_roles(user_id, role_id, company_id)` para la empresa del registro.
4. **Usuario específico.** Si se asigna una persona, solo esa persona firma. El selector puede
   filtrarse primero por rol, pero se persiste el `user_id`; un cambio posterior de rol no convierte
   silenciosamente a otra persona en la responsable.
5. **Personas distintas por etapa**, activado por defecto. Quien firmó una etapa no puede firmar otra
   del mismo intento, aunque tenga ambos roles. La definición puede permitirlo explícitamente para
   empresas pequeñas.
6. **El creador no se autoaprueba**, activado por defecto. También debe ser configurable por flujo.
7. **Acceso acotado al registro asignado.** Ser validador no concede menús, granjas ni acceso general
   a otra empresa. Sí concede, mientras la etapa esté asignada, lectura del detalle y la acción
   correctiva sobre ese seguimiento concreto. Editar o eliminar por el flujo se autoriza contra la
   instancia, no como un permiso CRUD abierto para todo el módulo.
8. **La última etapa es la única que aplica efectos.** Las firmas intermedias solo cambian el estado
   del flujo; no descuentan alimento ni aves.
9. **Devolución con novedad obligatoria.** El botón se llamará **Devolver para corrección**. No cierra
   el flujo: mueve el trabajo exactamente una etapa hacia atrás y registra quién lo devolvió, desde
   qué etapa y por qué.
10. **Destinatario concreto.** Si la etapa N devuelve, la corrección queda a cargo de la persona que
    firmó la etapa N−1. Si se devuelve la etapa 1, queda a cargo del creador del seguimiento. En Fase 1
    hay una firma por etapa; si después existe quorum, se notificará a cada firmante que deba rehacerla.
11. **Retroceso sucesivo.** Quien recibe una devolución puede: (a) editar y reenviar hacia la etapa que
    la devolvió; (b) devolverla una etapa más atrás con una nueva novedad; o (c) eliminar el
    seguimiento. Así una devolución de Costos puede bajar a Técnico, luego a Líder y finalmente al
    creador si ninguno debe corregirla directamente.
12. **Edición en etapa.** El responsable de la etapa actual puede corregir el seguimiento antes de
    aprobar. Toda modificación se audita. Si corrige una devolución, se recalculan las reservas y se
    reabre la etapa que devolvió; las aprobaciones anteriores al punto de retroceso permanecen.
13. **Reservas durante la devolución.** La devolución no libera por sí sola alimento/aves: el registro
    sigue vigente y debe conservar su separación. Editar reescribe las reservas con los valores
    corregidos; eliminar sí las libera y cancela el flujo en la misma transacción.
14. **Novedad persistente, no toast.** La persona a cargo de corregir recibe una tarjeta roja en el
    Home con empresa/proceso, granja, núcleo, galpón, lote, fecha del seguimiento, etapa que devolvió,
    persona y descripción. Marcarla como leída no la oculta.
15. **Resolución automática de la novedad.** El aviso desaparece solo cuando el destinatario corrige
    y reenvía, devuelve una etapa más atrás o elimina el seguimiento. Abrirlo o navegar al registro no
    lo resuelve.
16. **Borrado.** Eliminar un pendiente cancela la instancia, libera reservas y resuelve todas sus
    novedades activas dentro de la misma transacción.
17. **Desvalidar.** La corrección administrativa de un registro ya finalizado conserva el permiso
    especial actual, devuelve los efectos y crea un intento nuevo; nunca reutiliza firmas anteriores.
18. **Una acción por clic.** Aunque una persona sea candidata en dos etapas consecutivas, una petición
    aprueba una sola etapa. No existe avance automático por varias etapas.
19. **Aprobaciones en línea.** En Fase 1 aprobar/devolver no se guarda en el outbox offline: necesita
    leer y bloquear la etapa actual para evitar carreras. La captura del seguimiento conserva su
    soporte offline actual.
20. **Plazo configurable por flujo.** El valor inicial será 24 horas, igual al comportamiento actual.
    El vencimiento se calcula sobre la instancia completa en Fase 1; SLA por etapa queda preparado
    para una fase posterior.
21. **Límite técnico.** Entre 1 y 20 etapas. No se codifica un máximo de 3 o 4.

## 5. Estados y transiciones

### 5.1 Definición del flujo

- `BORRADOR`: editable; no afecta registros.
- `PUBLICADO`: inmutable y usado por registros nuevos.
- `RETIRADO`: no crea instancias nuevas; las existentes conservan su versión.

Modificar un flujo publicado significa **clonarlo a una versión nueva**, editar el borrador y
publicarlo. Nunca se cambia debajo de instancias en curso.

### 5.2 Instancia del registro

- `PENDIENTE_VALIDACION`: espera la etapa actual.
- `DEVUELTA_CORRECCION`: espera que el firmante anterior —o el creador— corrija, vuelva a devolver o
  elimine.
- `APROBADA`: todas las etapas terminaron y los efectos se aplicaron.
- `CANCELADA`: el registro fue eliminado o la instancia se anuló administrativamente.
- `ERROR_FINALIZACION`: la última firma no pudo aplicar efectos. La transacción se revierte, el
  seguimiento continúa sin validar y puede reintentarse sin duplicar firmas ni descuentos.

### 5.3 Etapa de una instancia

- `BLOQUEADA`: existe, pero una etapa anterior sigue abierta.
- `PENDIENTE`: etapa actual.
- `APROBADA`
- `DEVUELTA`: la etapa siguiente la regresó para corrección.
- `ESPERANDO_CORRECCION`: etapa que detectó la novedad y espera el reenvío.
- `CANCELADA`

### 5.4 Ejemplo de retroceso

Flujo: Creador → Líder (1) → Técnico (2) → Costos (3).

1. Líder aprueba: etapa 1 `APROBADA`, etapa 2 `PENDIENTE`.
2. Técnico detecta un error y devuelve: etapa 1 `DEVUELTA`, etapa 2
   `ESPERANDO_CORRECCION`, instancia `DEVUELTA_CORRECCION`.
3. Se crea una novedad activa para el Líder con la ubicación exacta y el motivo del Técnico.
4. El Líder puede editar y reenviar: la etapa 1 vuelve a `APROBADA`, la novedad se resuelve y la
   etapa 2 vuelve a `PENDIENTE`.
5. Si el Líder considera que debe corregir el creador, devuelve otra vez: se crea una novedad para el
   creador y la suya queda resuelta como `DEVUELTA_ATRAS`.
6. El creador corrige y reenvía o elimina. El seguimiento solo desaparece del circuito al eliminar;
   corregir lo devuelve a la primera etapa.

## 6. Modelo de datos

Todas las tablas usan `snake_case`, FKs reales cuando la entidad no es polimórfica, timestamps UTC e
índices por empresa/estado. Las migraciones deben ser idempotentes.

### 6.1 `validacion_procesos`

Catálogo global controlado por desarrollo; no es editable libremente por una empresa.

| Columna | Uso |
|---|---|
| `id` | PK |
| `key` | único: `SEGUIMIENTO_LEVANTE`, `SEGUIMIENTO_PRODUCCION`, etc. |
| `nombre`, `descripcion` | texto de UI |
| `adapter_key` | adaptador backend registrado |
| `menu_route` | ruta funcional asociada, para validar disponibilidad de la empresa |
| `is_active` | permite retirar un adaptador sin borrar historia |
| `orden` | orden de presentación |

Seed inicial por `key`, nunca por id fijo.

### 6.2 `validacion_flujos`

| Columna | Uso |
|---|---|
| `id` | PK |
| `company_id` | empresa dueña |
| `proceso_id` | FK al catálogo |
| `version` | consecutivo por empresa/proceso |
| `nombre` | nombre legible de la secuencia |
| `estado` | `BORRADOR` / `PUBLICADO` / `RETIRADO` |
| `plazo_total_horas` | default 24 |
| `requiere_personas_distintas` | default `true` |
| `permite_aprobacion_creador` | default `false` |
| `created_by_user_id`, `published_by_user_id` | FK GUID a `users` |
| `created_at`, `updated_at`, `published_at`, `retired_at` | auditoría |

Restricciones:

- unique `(company_id, proceso_id, version)`;
- índice único parcial: una sola versión `PUBLICADO` vigente por empresa/proceso;
- un flujo publicado no acepta `UPDATE` de pasos/asignados desde el servicio.

### 6.3 `validacion_flujo_pasos`

| Columna | Uso |
|---|---|
| `id`, `flujo_id` | PK/FK |
| `orden` | 1..20, único dentro del flujo |
| `nombre` | “Administración de granja”, “Técnico”, “Costos” |
| `aprobaciones_requeridas` | Fase 1 = 1; deja preparado quorum futuro |
| `descripcion` | instrucción opcional al validador |

### 6.4 `validacion_flujo_asignados`

Una fila por candidato de una etapa.

| Columna | Uso |
|---|---|
| `id`, `paso_id` | PK/FK |
| `tipo` | `ROL` o `USUARIO` |
| `role_id` | obligatorio para `ROL`, null para `USUARIO` |
| `user_id` | obligatorio para `USUARIO`, null para `ROL` |
| `rol_filtro_origen_id` | opcional, solo auditoría de cómo se eligió al usuario |

Un `CHECK` exige exactamente un responsable según `tipo`. Al publicar se valida que el rol pertenezca
a la empresa del flujo y que el usuario esté activo y asignado a esa empresa.

### 6.5 `validacion_instancias`

Una ejecución concreta del flujo sobre un registro de negocio.

| Columna | Uso |
|---|---|
| `id` | UUID o bigint; preferible UUID para futuras capturas offline |
| `company_id`, `proceso_id`, `flujo_id` | dueño, proceso y versión exacta |
| `recurso_tipo` | tipo estable del adaptador |
| `recurso_id` | string de hasta 64; admite ids bigint o GUID futuros |
| `intento` | 1, 2, 3... después de una cancelación/reapertura administrativa |
| `estado` | estados de §5.2 |
| `paso_actual_orden` | etapa habilitada |
| `paso_retorno_orden` | etapa que devolvió y a la que debe regresar tras corregir |
| `created_by_user_id` | creador del registro/intento |
| `created_at`, `updated_at`, `completed_at`, `returned_at`, `cancelled_at` | auditoría |
| `motivo_estado` | última devolución/cancelación/error legible |

Índice único parcial: una sola instancia activa (`PENDIENTE_VALIDACION` o `DEVUELTA_CORRECCION`) por
`(company_id, proceso_id, recurso_tipo, recurso_id)`.

### 6.6 `validacion_instancia_pasos`

Materializa el estado de cada etapa y conserva los valores relevantes de la definición para consulta
rápida y auditoría.

| Columna | Uso |
|---|---|
| `id`, `instancia_id`, `paso_definicion_id` | PK/FKs |
| `orden`, `nombre`, `aprobaciones_requeridas` | snapshot |
| `estado` | §5.3 |
| `opened_at`, `completed_at` | auditoría |

Los candidatos se leen de la definición inmutable. No se expanden todos los usuarios de un rol al
crear la instancia: el rol representa una responsabilidad viva y se evalúa contra `user_roles` de la
empresa al momento de firmar.

### 6.7 `validacion_acciones`

Bitácora append-only de firmas y decisiones.

| Columna | Uso |
|---|---|
| `id`, `instancia_id`, `instancia_paso_id` | trazabilidad |
| `accion` | `APROBAR`, `DEVOLVER`, `EDITAR`, `REENVIAR`, `ELIMINAR`, `CANCELAR`, `OVERRIDE` |
| `user_id` | FK GUID al usuario real; no usar el hash numérico de `ICurrentUser.UserId` |
| `asignado_id` | candidato rol/usuario que autorizó la firma |
| `comentario` | obligatorio al devolver/override; opcional al aprobar/editar |
| `created_at` | timestamp inmutable |

Un índice/constraint impide que la misma persona firme dos veces la misma etapa. El servicio añade la
restricción de personas distintas entre etapas cuando el flujo lo exige.

### 6.8 `validacion_novedades_usuario`

Aviso persistente y accionable del Home. No se deriva solo del historial porque necesita destinatario,
estado de resolución y contexto suficiente aun si el seguimiento se elimina.

| Columna | Uso |
|---|---|
| `id`, `instancia_id`, `accion_devolucion_id` | trazabilidad al flujo y a la devolución |
| `company_id` | aislamiento y filtro de empresa activa |
| `destinatario_user_id` | firmante anterior o creador que debe actuar |
| `generada_por_user_id` | quien devolvió |
| `estado` | `ACTIVA`, `RESUELTA`, `CANCELADA` |
| `motivo` | descripción obligatoria escrita al devolver |
| `contexto_resumen` | JSONB snapshot: granja, núcleo, galpón, lote, fecha, proceso y ruta |
| `created_at`, `read_at`, `resolved_at` | creada, vista y resuelta |
| `resolved_by_user_id`, `resolucion` | `CORREGIDO_REENVIADO`, `DEVUELTO_ATRAS`, `ELIMINADO` |

`read_at` solo cambia la apariencia de “nuevo”; una fila `ACTIVA` continúa en el Home. La resolución
se escribe en la misma transacción que corregir/reenviar, devolver atrás o eliminar.

## 7. Autorización y alcance multiempresa

### 7.1 Administrar la configuración

Permisos nuevos:

- `flujos_validacion.ver`
- `flujos_validacion.gestionar`
- `flujos_validacion.override` — emergencia, no se entrega por defecto.

El permiso abre o modifica el módulo; **no autoriza una firma**.

- Super Admin / administrador global de aplicación puede elegir cualquier empresa permitida por la
  policy global actual.
- Un administrador interno solo configura la empresa activa y necesita
  `flujos_validacion.gestionar` habilitado en `company_permissions` y asignado a su rol.
- Toda consulta/escritura toma la empresa efectiva de `ActiveCompanyMiddleware` o valida
  explícitamente el `companyId`. Ante duda devuelve 403/vacío, nunca cae a otra empresa.

La pantalla se habilita con `company_menus` y `role_menus`, siguiendo el patrón del repositorio. No se
localiza ningún menú, empresa, rol o permiso por id fijo.

### 7.2 Aprobar, devolver o corregir

La autorización runtime exige simultáneamente:

1. el registro pertenece a la empresa activa;
2. la instancia está `PENDIENTE_VALIDACION` o `DEVUELTA_CORRECCION`, según la acción;
3. el paso solicitado es el paso actual;
4. el usuario está activo y coincide con un candidato `ROL` o `USUARIO`;
5. cumple las reglas de creador y persona distinta;
6. para aprobar/devolver coincide con un candidato; para corregir/eliminar es el destinatario de la
   novedad activa o el creador cuando la primera etapa fue devuelta.

La autorización del flujo permite leer/corregir **ese recurso exacto** mientras la acción esté
pendiente. No habilita consultas generales, otros lotes ni otra empresa. El adaptador vuelve a validar
empresa, granja y estado antes de delegar en el mismo servicio de dominio que usa la edición normal.

El Super Admin **no firma por ser Super Admin**. Debe estar asignado o usar el override explícito con
motivo auditable. Esto evita saltarse el control que se pretende crear.

## 8. Integración con la doble validación existente

### 8.1 Resolución del modo por empresa y proceso

Se reemplaza la decisión binaria por una resolución de modo:

1. Hay flujo publicado para empresa + proceso → `SECUENCIAL`.
2. No hay flujo, pero `requiere_validacion_seguimiento_diario = true` → `LEGACY_UN_PASO` con los
   permisos actuales.
3. No hay flujo ni flag → `INMEDIATO`, comportamiento histórico.

Esto permite activar Santa Reyes por módulo sin cambiar Sanmarino, Demo, Ecuador o Panamá, y sin
apagar de golpe el sistema legacy de otros seguimientos.

Se agrega una sobrecarga por proceso/módulo a `IValidacionSeguimientoService`; no se sigue consultando
solo el booleano de empresa en los Crud de Levante y Producción.

### 8.2 Creación

En la misma transacción que guarda el seguimiento y crea/reemplaza reservas:

- `SECUENCIAL`: crea la instancia usando la versión publicada vigente y deja el paso 1 pendiente;
- `LEGACY_UN_PASO`: conserva el comportamiento actual;
- `INMEDIATO`: descuenta y nace validado como hoy.

La versión queda congelada en la instancia. Publicar una versión nueva solo afecta registros nuevos.

### 8.3 Aprobaciones intermedias y final

Las aprobaciones intermedias no llaman a `AplicarAlimentoAsync`, `AplicarAvesAsync` ni
`TomarValidacionAsync`.

La última aprobación llama a un núcleo de finalización extraído del servicio existente, sin repetir
fórmulas ni crear movimientos por otro camino. Debe ocurrir dentro de la misma transacción que:

- bloquea la instancia/paso actual;
- registra la última firma;
- toma atómicamente el seguimiento;
- aplica alimento y aves;
- marca reservas `APLICADA`;
- escribe `validado = true`, `validado_at` y el último `validado_por` por compatibilidad;
- deja la instancia `APROBADA`.

Si una parte falla, todo se revierte. El registro no puede quedar `APROBADO` con inventario sin tocar.

### 8.4 Devolución, corrección y aviso

Al devolver desde la etapa N, una sola transacción:

- bloquea la instancia y comprueba que N siga siendo la etapa actual;
- registra la acción `DEVOLVER` con motivo;
- deja N en `ESPERANDO_CORRECCION`;
- deja N−1 en `DEVUELTA`, o identifica al creador si N=1;
- cambia la instancia a `DEVUELTA_CORRECCION` y guarda `paso_retorno_orden=N`;
- crea la novedad activa para el firmante anterior/creador con el snapshot de ubicación;
- mantiene las reservas `ACTIVA` porque el seguimiento continúa vigente.

Al corregir y reenviar:

- el adaptador valida y guarda los cambios por el camino normal;
- `SepararAsync` reescribe las reservas con los valores corregidos;
- se registra `EDITAR`/`REENVIAR`;
- se resuelve la novedad;
- la etapa corregida vuelve a `APROBADA` y la etapa que devolvió vuelve a `PENDIENTE`; si la primera
  etapa fue devuelta al creador, el reenvío deja esa etapa 1 en `PENDIENTE` para que su validador la
  firme otra vez;
- la instancia vuelve a `PENDIENTE_VALIDACION`.

Al devolver otra vez hacia atrás se resuelve la novedad actual como `DEVUELTO_ATRAS` y se crea otra
para el responsable anterior. Al eliminar se cancelan instancia/etapas/novedades y se liberan las
reservas en la misma transacción.

### 8.5 Endpoints legacy

`POST .../validar` y `validar-pendientes` se conservan para empresas sin flujo secuencial. Cuando el
registro tiene una instancia secuencial, deben rechazar el atajo y dirigir al endpoint de aprobación;
nunca pueden saltar etapas porque el usuario conserve el permiso antiguo.

El botón “Validar todos” actual no puede conservar su significado bajo un flujo de tres personas. Se
reemplaza por “Aprobar mis pendientes”, que solo firma la etapa actual de registros donde el usuario
es candidato. No encadena etapas ni finaliza registros que estén esperando a otro responsable.

### 8.6 Activación sin mezclar pendientes

Publicar el primer flujo de una empresa/proceso ejecuta un preflight. Si existen seguimientos legacy
pendientes sin instancia, la publicación queda bloqueada hasta que se validen o se migren de forma
explícita. No se asigna retroactivamente una secuencia nueva a registros que nacieron bajo otra regla.

## 9. Backend propuesto

### 9.1 Application

- `Calculos/FlujoValidacionCalculos.cs`: valida topología, orden, estados y transiciones.
- `Calculos/FlujoValidacionAutorizacionCalculos.cs`: alcance global/empresa y reglas de firma.
- `DTOs/FlujosValidacion/`: definición, pasos, asignados, estado, historial y comandos.
- `Interfaces/IFlujoValidacionService.cs`.
- `Interfaces/IProcesoValidacionAdapter.cs`.

Todo cálculo de estado/autorización que no necesite EF será puro y tendrá xUnit.

### 9.2 Domain

Entidades para las ocho tablas de §6, enums/constantes de estado y navegaciones. No contienen
dependencias de EF ni lógica de infraestructura.

### 9.3 Infrastructure

```text
Services/FlujosValidacion/
├── FlujoValidacionService.cs                 # partial ancla, ctor y helpers compartidos
├── Adaptadores/
│   └── SeguimientoPosturaValidacionAdapter.cs
└── Funciones/
    ├── FlujoValidacionService.Configuracion.cs
    ├── FlujoValidacionService.Publicacion.cs
    ├── FlujoValidacionService.Instancias.cs
    ├── FlujoValidacionService.Aprobacion.cs
    ├── FlujoValidacionService.Devoluciones.cs
    ├── FlujoValidacionService.Novedades.cs
    └── FlujoValidacionService.Consultas.cs
```

Namespace plano `ZooSanMarino.Infrastructure.Services`, interfaz solo en el archivo ancla y consultas
filtradas en SQL por empresa/estado.

Cambios acotados en:

- `ValidacionSeguimientoService`: resolver modo y exponer el finalizador interno reutilizable.
- Crud de `SeguimientoLoteLevanteService` y `ProduccionService.Seguimiento`: crear/cancelar/reiniciar
  instancia junto con reservas.
- `ZooSanMarinoContext`, configuraciones EF y `Program.cs` para DI.

### 9.4 API

Dos controladores, sin `admin` en la ruta porque el WAF del proyecto bloquea ese término:

**Configuración** — `api/FlujosValidacion`

- `GET /procesos?companyId=`
- `GET ?companyId=&procesoKey=`
- `POST /borradores`
- `PUT /{flujoId}` — solo borrador
- `POST /{flujoId}/clonar`
- `POST /{flujoId}/publicar`
- `POST /{flujoId}/retirar`
- `GET /asignables?companyId=` — roles/usuarios acotados a la empresa

**Ejecución** — `api/ValidacionesFlujo`

- `GET /{procesoKey}/{recursoId}` — estado, línea de tiempo y acciones disponibles para la sesión.
- `POST /{instanciaId}/aprobar`
- `POST /{instanciaId}/devolver` — motivo obligatorio; retrocede una etapa.
- `POST /{instanciaId}/corregir-y-reenviar` — edición acotada al recurso devuelto.
- `DELETE /{instanciaId}/recurso` — eliminación acotada, cancela y libera.
- `GET /mis-pendientes?companyId=&procesoKey=`
- `GET /mis-novedades` — avisos activos del Home para el usuario/empresa actual.
- `POST /novedades/{novedadId}/marcar-leida` — no la resuelve ni la oculta.
- `POST /aprobar-lote` — opcional, solo mis etapas actuales; límite y resultado por fila.

La API responde 403 por falta de asignación, 409 por versión/etapa que cambió durante la operación y
400 por regla de negocio. Los endpoints mutables tienen tests de autorización, concurrencia e
idempotencia.

## 10. Frontend propuesto

### 10.1 Módulo de configuración

Ruta sugerida: `config/flujos-validacion`; menú: **Configuración › Flujos de validación**.

Pantalla:

1. Selector de empresa para el administrador global; fijo a empresa activa para el administrador
   interno.
2. Tarjetas de procesos implementados y disponibles para esa empresa.
3. Estado de cada flujo: Sin configurar / Borrador / Publicado / Retirado.
4. Constructor de secuencia:
   - cantidad de etapas (1..20);
   - acordeón por etapa;
   - nombre de etapa;
   - selector Rol / Usuario;
   - posibilidad de agregar varios candidatos;
   - resumen visual 1 → 2 → 3;
   - reglas “personas distintas” y “creador puede aprobar”;
   - plazo total.
5. Guardar borrador, previsualizar, publicar, clonar versión y retirar.
6. Preflight visible con problemas concretos: etapa vacía, usuario inactivo, rol de otra empresa,
   pendientes legacy, falta de acceso funcional, etc.

Componentes nuevos con `changeDetection: ChangeDetectionStrategy.Eager` explícito. Funciones puras
para construir/validar la secuencia en `funciones/`; modelos compartidos en `models/`.

### 10.2 Seguimiento diario de Levante y Producción

Junto al estado de cada seguimiento:

- `Pendiente · etapa 1 de 3`
- nombre de la etapa actual;
- candidatos legibles;
- vencimiento;
- botón **Confirmar etapa** solo si la API declara `puedeAprobar=true`;
- botón **Editar** para el responsable actual, acotado a ese seguimiento;
- botón **Devolver para corrección** con modal y novedad obligatoria;
- estado rojo **Devuelto** para el responsable anterior;
- acciones **Corregir y reenviar**, **Devolver una etapa más** o **Eliminar seguimiento**;
- línea de tiempo: quién firmó, fecha/hora y comentario;
- `Validado` únicamente después de la finalización real.

La UI no calcula autorización a partir de nombres de roles. Renderiza las acciones que devuelve el
backend y usa los permisos solo para entrar a las pantallas de configuración.

### 10.3 Bandeja personal

El mismo módulo incluye **Mis validaciones pendientes**, filtrable por empresa, proceso, granja,
fecha y estado. No se incorpora correo/push en esta fase; el sistema de correo tiene dependencias
operativas externas y no es necesario para garantizar la secuencia.

### 10.4 Novedades persistentes en el Home

El Home ya compone paneles autónomos (`PanelPendientesFirmaComponent` y pendientes de vacunación) y
centraliza su aspecto en `shared/styles/pendientes-panel.scss`. Se agrega
`PanelNovedadesValidacionComponent`, con `ChangeDetectionStrategy.Eager`, sin mezclarlo con los otros
dominios.

Cada tarjeta roja muestra:

- “Seguimiento devuelto por Técnico” y la etapa desde la que volvió;
- empresa/proceso;
- granja, núcleo, galpón, lote y día exacto;
- nombre de quien lo devolvió y fecha/hora;
- descripción completa de la novedad;
- botón **Ir al seguimiento** con navegación directa al lote/registro;
- indicador “Nuevo” basado en `read_at`, sin botón para descartar el aviso.

El panel consulta `mis-novedades` al cargar Inicio, se oculta si no hay activas y se actualiza al
cambiar de empresa. Corregir y reenviar, devolver atrás o eliminar resuelve la tarjeta en backend; al
volver al Home ya no aparece. Visitarla sin corregirla no la elimina.

## 11. Migraciones y seeds

1. `AddFlujosValidacionParametrizables`:
   - ocho tablas, incluida `validacion_novedades_usuario`;
   - FKs, CHECKs e índices;
   - DDL idempotente.
2. `SeedProcesosValidacionInicial`:
   - `SEGUIMIENTO_LEVANTE` y `SEGUIMIENTO_PRODUCCION` por key;
   - data-only, sin ids fijos.
3. `SeedMenuYPermisosFlujosValidacion`:
   - menú por `route`;
   - permisos por `key`;
   - clasificación en el módulo de permisos correspondiente;
   - `company_menus`/`company_permissions` sin otorgar firmas runtime.

No se crea por migración una secuencia con nombres de personas de Santa Reyes. Los roles/usuarios
reales se eligen desde la pantalla después del despliegue. Si el cliente pide una configuración
inicial reproducible, se agrega luego una migración data-only separada, localizada por empresa,
rol y correo/nombre estable, y aprobada antes de desplegar.

## 12. Fases de ejecución

### F0 — Cierre funcional y diseño (antes de código)

- confirmar las reglas propuestas de §4;
- confirmar los nombres reales de los tres responsables de Santa Reyes y si son roles o usuarios;
- inventariar pendientes legacy de Santa Reyes antes de publicar;
- congelar contratos API y wireframes.

### F1 — Motor persistente y configuración backend

- entidades/configuraciones/migración;
- catálogo de procesos;
- CRUD de borradores y versiones;
- publicación/preflight;
- autorización global/empresa;
- cálculos puros y tests.

### F2 — Ejecución transaccional

- crear instancias;
- aprobar/devolver/corregir/reenviar/eliminar/cancelar;
- novedad persistente y resolución transaccional;
- concurrencia e idempotencia;
- adaptador de seguimiento de postura;
- reutilizar finalizador actual sin duplicar fórmulas;
- integración con reservas y edición/borrado.

### F3 — Constructor Angular

- ruta/menú;
- selector de empresa y proceso;
- editor de etapas/asignados;
- preflight, publicación, clonado y retiro;
- bandeja personal y panel de novedades en Home.

### F4 — Levante y Producción

- badge y línea de tiempo junto a cada seguimiento;
- confirmar/editar/devolver/corregir/reenviar/eliminar;
- fila roja, novedad y navegación directa desde Home;
- adaptar validación en bloque;
- flujos de edición, borrado y desvalidación;
- mantener referencias estables y `Eager` explícito.

### F5 — Validación y activación controlada

- build/tests;
- migración sobre clon local, segunda pasada idempotente y rollback;
- smoke sin flujo, legacy y secuencial;
- publicar primero los dos flujos de Santa Reyes desde la UI;
- verificación post-deploy antes de declarar activo el cambio.

## 13. Casos de prueba obligatorios

### Regresión

1. Empresa sin flujo y flag OFF: guardar/editar/borrar aplica exactamente como hoy.
2. Empresa sin flujo y flag ON: continúa la validación legacy de un paso.
3. Engorde y Reproductora no cambian en Fase 1.

### Configuración

4. Administrador interno solo ve/configura su empresa.
5. Administrador global cambia de empresa explícitamente.
6. No se publica con 0 etapas, órdenes repetidos o etapa sin candidato.
7. No se asigna un rol/usuario ajeno o inactivo.
8. Publicado es inmutable; clonar crea versión N+1.
9. Una versión nueva no cambia instancias existentes.
10. Publicación se bloquea con pendientes legacy no resueltos.

### Flujo de tres etapas

11. Guardar crea seguimiento, reservas e instancia; inventario/aves no se mueven.
12. Usuario sin asignación recibe 403 aunque tenga el antiguo permiso `*.validar`.
13. Candidato de etapa 2 no puede firmar mientras la 1 esté pendiente.
14. Un rol candidato funciona solo si el usuario lo tiene en la misma empresa.
15. Entre varios roles/usuarios candidatos, una firma completa la etapa `ANY`.
16. El creador no firma si la definición no lo permite.
17. La misma persona no firma dos etapas si se exigen personas distintas.
18. Etapas 1 y 2 no crean movimientos ni modifican `validado`.
19. Etapa 3 aplica una sola vez reservas, alimento, bajas y marca final.
20. Dos clics concurrentes en la última etapa producen un solo descuento.
21. Falla de stock en la etapa final revierte firma, estado y movimientos.

### Corrección

22. Editar sin firmas reescribe reservas y conserva etapa 1.
23. Técnico devuelve la etapa 2: la etapa 1 queda roja/devuelta y el Líder recibe la novedad.
24. La devolución exige motivo y mantiene las reservas activas.
25. Líder corrige: se reescriben reservas, se resuelve su novedad y vuelve a Técnico, no a Costos.
26. Líder devuelve hacia atrás: su novedad se resuelve y nace otra para el creador.
27. Devolver la etapa 1 apunta al creador del seguimiento.
28. El responsable actual puede editar directamente y la acción queda auditada.
29. Borrar desde una devolución cancela, libera reservas y resuelve todas las novedades.
30. Desvalidar devuelve efectos e inicia una secuencia nueva.

### Multiempresa y seguridad

31. Id de recurso de otra empresa devuelve 404/403 fail-closed.
32. Cambiar headers no permite firmar, corregir ni leer novedades de otra empresa.
33. La autorización correctiva solo sirve para el recurso exacto devuelto.
34. Super Admin no puede firmar sin asignación salvo override explícito y auditado.
35. La bandeja y el Home solo devuelven filas de usuario y empresa activa.

### Frontend

36. Se muestran etapa X de N, responsable, tiempo e historial correctos.
37. La fila devuelta se marca roja para quien debe actuar.
38. Home muestra granja/núcleo/galpón/lote/día, autor y motivo de la devolución.
39. Marcar la novedad como leída no la oculta; corregir/reenviar sí la resuelve.
40. El botón desaparece o queda inactivo al avanzar la etapa.
41. Abrir/cerrar dos veces cada modal conserva datos y apaga spinners.
42. Payloads de aprobar/devolver/corregir/reenviar coinciden con el contrato API.

## 14. Validación técnica obligatoria

- `cd backend && dotnet build`
- `cd backend && dotnet test`
- tests de integración de controllers/handlers y concurrencia real sobre PostgreSQL;
- `node backend/scripts/verificar-sql-llega-por-migracion.js`;
- aplicar migraciones en BD local, repetir y probar `Down`/`Up` cuando sea seguro;
- `cd frontend && yarn build`;
- specs focalizados del constructor y las dos pantallas;
- smoke doble:
  - Sanmarino/Demo sin flujo: cero cambios;
  - empresa que conserve legacy: un paso intacto;
  - Santa Reyes con 3 etapas: ningún efecto antes de la tercera y aplicación única al final;
- puertos/procesos libres al terminar.

## 15. Criterios de aceptación de la primera fase

La fase se considera terminada cuando:

- un administrador autorizado puede crear y publicar desde la UI un flujo de 1..20 etapas para
  Levante o Producción en una empresa;
- cada etapa admite uno o varios roles/usuarios de esa empresa;
- las etapas se cumplen en orden y cada firma queda auditada;
- el viejo permiso de validar no salta un flujo secuencial;
- el seguimiento se guarda y reserva, pero alimento/aves no se aplican hasta la última firma;
- la última firma reutiliza el finalizador actual de manera atómica e idempotente;
- una devolución retrocede una etapa, identifica al responsable real y conserva reservas;
- cada responsable puede corregir/reenviar, devolver hacia atrás o eliminar;
- el Home mantiene una novedad roja con ubicación y motivo hasta resolverla;
- edición, devolución, borrado y reenvío no dejan reservas ni avisos huérfanos;
- Santa Reyes puede operar el ejemplo Administrador de granja → Técnico → Costos;
- las empresas sin flujo y los módulos fuera de Fase 1 permanecen idénticos.

## 16. Extensión a nuevos módulos

Para incorporar un proceso futuro se exige el siguiente contrato:

1. registrar una key estable en `validacion_procesos` mediante migración;
2. implementar un `IProcesoValidacionAdapter` que resuelva empresa desde los datos;
3. separar o dejar pendiente el efecto al crear;
4. finalizar de forma transaccional e idempotente;
5. conservar/recalcular la separación al devolver/corregir y liberar al borrar/cancelar;
6. exponer un resumen seguro para la bandeja;
7. agregar tests con flujo OFF/legacy/secuencial;
8. habilitarlo en la UI solo después de que el adaptador esté desplegado.

Orden sugerido después de la Fase 1: ingresos de inventario → movimientos de huevos → movimientos de
aves → ventas. Cada integración es una fase propia; no se mezclan todas con la construcción inicial
del motor.

## 17. Riesgos y mitigaciones

- **Bypass por endpoint viejo:** cortar el endpoint legacy cuando exista instancia secuencial.
- **Editar después de aprobar:** registrar la edición y reabrir desde el punto exacto de devolución;
  las etapas posteriores no avanzan hasta que quien devolvió revise otra vez.
- **Roles cambiantes:** definición versionada y evaluación por empresa; usuarios específicos siguen
  siendo específicos.
- **Aprobación duplicada:** lock de instancia/paso + constraint + finalizador atómico existente.
- **Reserva incorrecta durante devolución:** mantenerla activa y reescribirla atómicamente al editar;
  liberar solo al eliminar/cancelar.
- **Aviso leído pero no resuelto:** `read_at` no cambia `estado`; solo una acción de negocio lo cierra.
- **Aviso al rol equivocado:** la devolución apunta al firmante real de la etapa anterior, no a todos
  los usuarios que hoy tengan ese rol.
- **Flujo publicado mal configurado:** borrador + preflight + publicación explícita; no editar en vivo.
- **Administrador que se salta el control:** sin bypass implícito; override separado, motivo obligatorio.
- **Confundir permiso con firma:** permisos gobiernan pantalla/configuración; asignaciones gobiernan
  aprobación.
- **Empresa equivocada:** resolver siempre desde el recurso y empresa activa validada, fail-closed.
- **Activación con pendientes viejos:** preflight y corte hasta sanear/migrar.
- **Despliegue front/back no simultáneo:** desplegar motor e UI primero; publicar el flujo de Santa
  Reyes solo cuando ambas versiones estén verificadas.

## 18. Estimación inicial

El alcance completo de Fase 1 es mayor que “agregar un tercer botón”: incluye motor versionado,
autorización por empresa, auditoría, retroceso escalonado, novedades persistentes en Home, constructor
UI y dos integraciones críticas. Estimación técnica preliminar: **124–156 horas** de desarrollo y validación, antes de contingencias de
datos o definiciones del cliente. Debe recalibrarse al cerrar F0 y medir pendientes/roles reales de
Santa Reyes.
