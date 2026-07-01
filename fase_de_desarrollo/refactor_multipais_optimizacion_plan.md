# Plan — Refactorización y optimización multi-país (rama `refactor/optimizacion-multipais`)

> **Objetivo:** análisis profundo y mejora iterativa de todos los módulos multi-país: eliminar código muerto, unificar lógica compartida con parametrización por país, mover consultas pesadas del backend a funciones/vistas de BD (reducir consumo), y limpiar tablas/objetos de BD sin uso — **sin cambiar comportamiento ni tocar `main`**.

---

## 1. Enfoque arquitectónico

- **Rama de trabajo:** `refactor/optimizacion-multipais` (creada desde `main`). `main` no se toca; merge solo al final con OK explícito.
- **Refactor ≠ cambio de comportamiento:** UI, contratos API, aritmética y redondeos idénticos. Patrón canónico: `movimientos-pollo-engorde` (front `funciones/` + `models/`; back `partial class` en `Funciones/` + cálculo puro en `Application/Calculos/`).
- **Loop de mejora:** ciclos cortos. Cada ciclo = 1 hallazgo cerrado → build back + front → validación visual en preview (app corriendo) → commit en la rama → siguiente hallazgo. Al terminar la pasada completa, se lanza una **segunda pasada** de búsqueda de mejoras sobre lo ya refactorizado.
- **BD:** el **código actual manda** (regla de schema del CLAUDE.md). Nada de DDL en prod; todo se prueba contra el Docker local (`zoo_sanmarino_db`). Limpieza de tablas = solo reporte + script propuesto; el DROP real requiere OK explícito del usuario.

## 2. Mapa multi-país (inventario)

**Módulos por país (hoy duplicados o exclusivos):**

| Dominio | Colombia (base) | Ecuador | Panamá |
|---|---|---|---|
| Seguimiento engorde | `SeguimientoAvesEngordeService` + front `aves-engorde` | `SeguimientoAvesEngordeEcuadorService` | `SeguimientoAvesEngordePanamaService` + front `aves-engorde-panama` (clon casi 1:1) |
| Movimientos engorde | `MovimientoPolloEngorde/` (patrón canónico) | — | `MovimientoPolloEngordePanama/` + `venta-panama-pollo-engorde.service.ts` |
| Liquidación técnica | `LiquidacionTecnicaService` | `LiquidacionTecnicaEcuadorService` (+ vista `vw_liquidacion_ecuador_pollo_engorde`) | (usa reporte indicador) |
| Guía genética | `GuiaGeneticaService` (`guia_genetica_sanmarino_colombia`) | `GuiaGeneticaEcuadorService` | pendiente (informe semanal) |
| Indicadores/reportes | reportes técnicos | `IndicadorEcuadorService` | `ReporteIndicadorPanamaService`, `InformeSemanalPolloEngordeService` |
| Inventario ítems | `CatalogoAlimentos` / `gestion-inventario` | `ItemInventarioEcuadorService` | — |
| Postura | `LotePostura{Base,Levante,Produccion}` | — | — |

**Parametrización existente:** `pais_id`/`company_id` en 22 entidades, `CompanyPais`, contexto de empresa activa en front. Es la base para unificar: un servicio + estrategia/parámetros por país en lugar de servicios clonados.

## 3. Fases del loop (orden de ejecución)

### Fase 0 — Línea base y validación (antes de tocar nada)
- Build backend (`dotnet build`) y frontend (`yarn build`) en la rama: registrar errores/advertencias actuales.
- `dotnet test` como línea base.
- Levantar entorno local (`make up`) para validación visual continua.

### Fase 1 — Código muerto (bajo riesgo, alto valor)
Candidatos ya confirmados:
- `backend/src/ZooSanMarino.Infrastructure/Services/managerUser.cs` — namespace `UserAdmin` autocontenido, **0 referencias externas** → eliminar.
- `frontend/src/app/features/test/` — componente de prueba sin ruta → eliminar.
Pendiente de barrido sistemático:
- Servicios back sin registro en DI o sin controller que los use.
- Componentes/servicios front sin ruta ni import.
- DTOs/modelos huérfanos; scripts SQL de diagnóstico ya aplicados (documentar, no borrar historial).

### Fase 2 — Unificación multi-país con parametrización
Por dominio, aplicando el patrón canónico (funciones puras + partial classes), **un dominio por ciclo**:
1. `aves-engorde` vs `aves-engorde-panama` (front): extraer funciones/modelos compartidos; el clon queda como orquestador delgado parametrizado por país.
2. `SeguimientoAvesEngorde{,Ecuador,Panama}Service` (back): cálculo puro común en `Application/Calculos/`, diferencias por país como parámetros/estrategia.
3. `MovimientoPolloEngorde` vs `...Panama`: mismo tratamiento.
4. Liquidaciones (Colombia/Ecuador): compartir el core de cálculo, mantener vistas Power BI intactas (**no renombrar** `vw_*` de Ecuador — dependencia Power BI).

### Fase 3 — Optimización BD: mover cómputo a funciones/vistas
- Identificar endpoints que arman agregaciones pesadas en C# (LINQ con múltiples Include/GroupBy sobre `seguimiento_diario_aves_engorde`, liquidaciones, indicadores) y migrarlas a funciones SQL (patrón existente: `fn_seguimiento_diario_engorde`, `fn_informe_semanal_pollo_engorde`, `fn_indicadores_pollo_engorde`).
- Cada función nueva: script en `/backend/sql/` + migración EF idempotente que la aplique + test de equivalencia (resultado C# previo == resultado SQL).
- Índices faltantes para los filtros más usados (lote, fecha, company_id, pais_id).

### Fase 4 — Normalización y limpieza de BD
- Cruzar `information_schema.tables` (local) contra entidades mapeadas (`Configurations/*.ToTable`) → listar tablas sin mapeo ni uso en SQL crudo/vistas.
- Detectar columnas legacy no mapeadas por EF.
- **Entregable: informe + script `DROP ... IF EXISTS` comentado.** Ejecución solo con OK explícito, primero en local.
- Respetar objetos vivos: vistas Power BI Ecuador, `espejo_huevo_produccion` + triggers, funciones `fn_*`.

### Fase 5 — Segunda pasada (loop de mejoras sobre lo refactorizado)
- Re-barrido completo: nuevos duplicados, oportunidades de simplificación, advertencias de build restantes.
- Cierre: resumen de cambios, diff contra `main`, checklist de regresión visual por módulo.

## 4. Reglas por ciclo del loop

1. Elegir el ítem más arriba del tracker que esté pendiente.
2. Implementar en unidades pequeñas (patrón canónico).
3. `dotnet build` + `yarn build` (0 errores, sin advertencias nuevas) + `dotnet test` si tocó cálculo.
4. Validación visual: levantar preview y verificar el módulo afectado (pantalla carga, datos iguales, acciones responden).
5. Commit atómico en la rama con mensaje descriptivo.
6. Marcar `- [x]` en `tracker_estado.md` y pasar al siguiente.
7. `make down` / detener procesos al cerrar cada sesión de trabajo.

## 5. Casos de prueba transversales

- Backend compila 0 errores; tests de `Application.Tests` verdes.
- Front compila producción sin errores nuevos.
- Módulo refactorizado: misma respuesta de API (payload idéntico en endpoints muestreados) y misma UI.
- Funciones SQL nuevas: equivalencia numérica exacta con el cálculo C# que reemplazan (mismos redondeos).
- BD local migra limpia desde cero (`dotnet ef database update`).

## 6. Riesgos y salvaguardas

- **Vistas Power BI Ecuador**: prohibido renombrar/eliminar (`vw_liquidacion_ecuador_pollo_engorde`, etc.).
- **WAF**: ninguna ruta nueva con "admin" en el path.
- **Migraciones**: siempre idempotentes; jamás tocar `__EFMigrationsHistory` a mano.
- **Change detection Angular**: no introducir getters que creen objetos nuevos por ciclo.
- **Prod intocable**: ni deploy ni DDL desde esta rama sin OK explícito.
