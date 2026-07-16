# 🔄 CONTEXTO DE TRASPASO — módulo de Vacunación (cronogramas por lote)

> Pegá este archivo (o su ruta) al abrir un chat nuevo para continuar sin perder nada.
> **Fecha de corte:** 2026-07-14 · **Sesión origen:** "Batch vaccination scheduling module" (archivada, sin rama ni código — ver §3).
> Working tree: **sin cambios de este módulo** (nada se llegó a tocar).

---

## 1) Qué es este trabajo

App San Marino (avícola multi-país): **backend .NET 10 LTS Clean Architecture** + **frontend Angular 22 standalone**, desplegada en **AWS ECS**. Monorepo. Reglas vinculantes en `CLAUDE.md` (raíz) — **workflow obligatorio**: STEP 1 plan en `fase_de_desarrollo/<feature>_plan.md`, STEP 2 tracker en `tracker_estado.md`, ANTES de escribir código.

**Pedido del usuario:** módulo nuevo (sin tablas de BD todavía) para programar y controlar el **cronograma de vacunación por lote/granja/galpón**, con perfiles de administración del cronograma y de registro de aplicación, más reportes/gráficas de cumplimiento. El usuario pidió explícitamente **crear una rama aparte** para este desarrollo (aún no se creó).

---

## 2) Requerimiento original (verbatim, fuente de verdad)

> quiero un modulo nuevo que es el encargado de realizar programacion por lote, por granja, por galpones, especificando la vacunación, donde se va a programar y tendremos varios perfiles donde tendremos el administrador de vacunación, que crea el cronograma de vacunación del lote, que va colocar en qué fecha se debe aplicar o a qué semana se debe aplicar si está en levante coloca la semana y si está en producción igual o solo una fecha sin importar la fase; en esta parte creará el cronograma de vacunación de todo el tiempo de vida del lote, entonces será muy limpio, solo cronogramas bien profesionales y con un buen diseño, donde verá las vacunas → Ítems de inventario (Ecuador) que se utiliza actualmente, ahí tendrá el concepto vacuna (debe estar la vacuna creada) y sobre esa se crea el cronograma de aplicación; por el momento no vive sobre inventario (no consume/descuenta stock), solo trae las vacunas del catálogo para evitar digitarlo.
>
> El otro módulo dentro de vacunación será el **cronograma** (registro de aplicación): aquí verán las fases, si se aplicó o no, donde el operador registra la fecha en que realizó la vacunación — esta fecha NO es dinámica/editable, solo captura la fecha del sistema. Si se aplicó días después mostrará los días después de la vacunación (tardanza); si se pasó de una o dos semanas, mostrará en rojo que en el lote no se cumplió el cronograma de vacunación — aun así se confirma que se aplicó, pero queda registrado como **tardío**. Si no se aplicó, se marca "no se aplicó" y debe dejar una **descripción/motivo obligatorio**. Si se aplica fuera de la fecha (antes o después del rango programado) también debe dejar descripción — puede haber un **rango de semana** (ej. semana 4 = lunes a domingo de esa semana) para poder inyectar; si se pasa de la semana, se habilita el registro "aplicado" pidiendo una descripción bien detallada del motivo, más el **id del usuario que registra** y el **usuario a cargo de la aplicación** (si no está en el sistema, se escribe el nombre a mano).
>
> Todo siempre sobre la misma empresa. Si el usuario está asociado a una empresa y una sola granja, solo ve su asignación/plan de esa granja; si tiene varias granjas asignadas, ve todos los planes por granja, bien organizado. Debe tener opción de **descargar en Excel** bien organizado.
>
> Tendremos además un módulo dentro de vacunación de **gráficas y reportes**: filtro por granja, lote, galpón o núcleo, para información de cumplimiento, y gráficas comparativas de cronograma vs. cumplimiento, pudiendo comparar con otros lotes de la misma granja u otras, para ver dónde se cumple más y dónde no.
>
> Son módulos y funciones nuevos porque **no existe base de datos todavía** para esto — se resolverá con **funciones de base de datos** (no cómputo en backend) para agilizar desarrollo, consultas y transformación de información. Usar el diseño que ya se lleva en la aplicación y los filtros que ya existen en otros módulos. **Crear una rama aparte** para este desarrollo.

### Desglose funcional (interpretación organizada, no reemplaza el texto de arriba)

1. **Catálogo de vacunas** — entidad "Vacuna" que referencia el ítem de inventario correspondiente (para no digitar de nuevo); el módulo de vacunación **no descuenta stock**, solo lee el catálogo.
2. **Administrador de cronograma** — perfil que arma el cronograma completo de vida del lote: por semana (Levante) o por fecha (Producción, sin importar fase), asociando cada vacuna a una semana/fecha objetivo + rango de aplicación (ej. semana N = lunes a domingo).
3. **Registro de aplicación** — perfil operador: confirma aplicación con fecha de sistema (no editable), calculando automáticamente si fue a tiempo, tardía (muestra días de atraso, rojo si pasa 1-2 semanas) o no aplicada. Exige descripción/motivo cuando: no se aplicó, o se aplicó fuera del rango programado. Guarda usuario que registra (id, del sistema) + responsable de aplicación (nombre libre si no es usuario del sistema).
4. **Visibilidad por asignación** — usuario ve solo las granjas que tiene asignadas (una o varias), siempre dentro de su empresa activa.
5. **Exportación Excel** de cronogramas/reportes.
6. **Reportes y gráficas de cumplimiento** — filtros granja/lote/galpón/núcleo, comparativas entre lotes (misma granja u otras).
7. **Todo el cómputo pesado (comparativas, cumplimiento) en funciones/vistas SQL**, no en memoria del backend.

---

## 3) Estado real — qué se hizo y qué NO

- **Código: nada.** No hay entidades, controllers, componentes ni SQL escritos. Confirmado: sin commits, sin cambios sin commitear en ningún worktree, sin archivos `*vacun*` en el repo.
- **Rama: no creada.** El usuario la pidió explícitamente ("crea una rama aparte para este caso").
- **Plan (`fase_de_desarrollo/<feature>_plan.md`): no existe** — STEP 1 del workflow obligatorio de `CLAUDE.md` está pendiente.
- **Tracker (`tracker_estado.md`): no tocado** para este tema (tiene contenido de otro fix, hay que reemplazarlo por completo cuando arranque este módulo, per STEP 2).
- **Lo que sí se hizo:** la sesión anterior entró en **modo plan**, lanzó 3 sub-agentes de investigación y ~500 llamadas de exploración (Read/Grep/Glob/Bash) sobre el repo — pero el detalle de esos hallazgos **no quedó recuperable** (el historial de traspaso solo expone qué herramienta se llamó, no sus argumentos/resultados). La sesión terminó llamando `AskUserQuestion` — es decir, iba a preguntar algo de aclaración al usuario — pero se archivó antes de que se viera o respondiera esa pregunta. El contenido de esa pregunta específica **se perdió**.

**Conclusión práctica:** hay que retomar la fase de aclaración de dudas desde cero (ver §4), no asumir que ya se resolvieron.

---

## 4) Dudas a resolver ANTES de escribir el plan (derivadas del texto, no recuperadas de la sesión perdida)

- **Rol "administrador de vacunación"**: ¿es un rol nuevo en el sistema de roles/permisos existente, o un permiso adicional sobre roles ya existentes?
- **Catálogo de vacunas**: ¿"Vacuna" es una entidad propia que referencia un `ItemInventario` (ver nota abajo sobre el rename), o simplemente se filtran los `ItemInventario` por categoría/tipo "Vacuna" sin tabla nueva?
- **Cronograma único vs. por fase**: ¿es un solo cronograma para "toda la vida del lote" que mezcla semanas (Levante) y fechas (Producción), o son dos cronogramas independientes que se muestran juntos?
- **Regla de rango semanal**: confirmar que semana N = lunes a domingo de esa semana de vida del lote, y qué pasa si se registra aplicación *antes* de que abra el rango (¿se permite con descripción, igual que aplicar tarde?).
- **Umbral de "tardío" vs. "incumplido en rojo"**: ¿es exactamente 1-2 semanas de atraso el corte para marcar rojo, o el usuario quiere un valor configurable?
- **Fecha de aplicación**: confirmar que SIEMPRE es la fecha del sistema al momento de registrar (no se permite backdating manual).
- **Responsable de aplicación**: dos campos separados — `usuario_registra` (login del sistema, obligatorio) y `aplicado_por` (texto libre si la persona no tiene usuario, o FK si sí lo tiene). Confirmar el diseño exacto.
- **Visibilidad por granja**: confirmar que reutiliza el mecanismo de asignación usuario↔granja ya existente en el sistema (revisar cuál es antes de crear uno nuevo).
- **Métricas de cumplimiento** para reportes/gráficas: ¿% aplicado a tiempo, % tardío, % no aplicado, promedio de días de atraso? Definir antes de diseñar las funciones SQL.
- **Exportables a Excel**: ¿cronograma completo del lote, historial de aplicaciones, reporte de cumplimiento — los tres, o solo alguno?
- **Confirmar explícitamente**: el módulo de vacunación NO descuenta inventario (solo lee el catálogo para poblar el selector) — a diferencia de alimento, que sí descuenta. Verificar que esto sea intencional y no se espere integrarlo a futuro.

---

## 5) Patrones del repo a reutilizar (de `CLAUDE.md`, no de la investigación perdida)

- **Estructura canónica de módulo limpio** (copiar el patrón): `movimientos-pollo-engorde` — front `funciones/` (funciones puras) + `models/`; back `partial class` por responsabilidad en `Funciones/` + cálculo puro en `Application/Calculos/`.
- **Catálogo de vacunas** → el módulo de inventario "Ecuador" que menciona el usuario ya fue renombrado de forma neutra: `ItemInventarioEcuador` → `ItemInventario` (compartido EC/PA/CO, ver memoria `inventario-rename-neutro.md`). Evaluar filtrar por categoría/tipo en vez de crear un catálogo paralelo, salvo que el usuario confirme que quiere una entidad "Vacuna" separada.
- **Export a Excel** → SIEMPRE `shared/utils/excel/exportar-tabla-excel.funcion.ts` (`exportarTablaExcel`/`exportarMultiHojaExcel`/etc.) — prohibido `XLSX.book_new`/`writeFile` inline.
- **Confirmaciones y notificaciones** → `ConfirmDialogService` / `ToastService` — prohibido `alert()`/`confirm()` nativos.
- **Filtros por granja/lote/galpón** ya existen en otros módulos (seguimiento diario levante/producción) — reutilizar componentes, no reinventar.
- **Cómputo pesado / comparativas entre lotes** → función o vista SQL en `/backend/sql/` (regla del `CLAUDE.md`: "el backend orquesta, la BD filtra"), consistente con el pedido explícito del usuario de resolver esto con funciones de BD.
- **Migraciones** → idempotentes (`CREATE TABLE IF NOT EXISTS`, etc.), siguiendo el flujo estándar de `dotnet ef migrations add`.

---

## 6) Próximo paso concreto

1. Abrir un chat nuevo, pegar este archivo.
2. Resolver con el usuario las dudas del §4 (usar `AskUserQuestion` en modo plan; no asumir respuestas).
3. **STEP 1**: escribir `fase_de_desarrollo/vacunacion_cronograma_plan.md` con enfoque arquitectónico, entidades/tablas nuevas, funciones SQL, endpoints, componentes front y casos de prueba.
4. **STEP 2**: reemplazar `tracker_estado.md` con el checklist granular de este plan.
5. Crear la rama aparte que pidió el usuario (nombre sugerido: `feature/modulo-vacunacion`, a confirmar).
6. Recién ahí empezar a tocar BD/código, siguiendo el patrón de `movimientos-pollo-engorde` (§5).
