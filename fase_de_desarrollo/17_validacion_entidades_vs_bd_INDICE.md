# 17 · Validación Entidades Backend ↔ Base de Datos · Alineación para Producción

**Fecha:** 2026-05-31 · **Objetivo:** dejar el backend 100% alineado con la BD de pruebas
(`sanmarinoapplocal`) para que, al desplegar a producción con `Database__RunMigrations=true`,
se cree automáticamente **todo** (tablas, funciones, triggers, vistas) sin depender de scripts
SQL manuales.

## Documentos de este paquete

| Doc | Contenido |
|-----|-----------|
| [Parte A — Mapeo](17_validacion_entidades_vs_bd_PARTE_A_mapeo.md) | Por cada tabla/vista de la BD: entidad asociada, PK, FKs salientes, tablas que la referencian y **todos sus campos** (tipo, nullabilidad, clave). 77 tablas + 3 vistas. |
| [Parte B — Auditoría](17_validacion_entidades_vs_bd_PARTE_B_auditoria.md) | Auditoría de funciones/triggers/vistas vs migraciones EF, desalineaciones detectadas y plan de migración. |

## Metodología

1. **Conexión a la BD local** (`localhost:5432/sanmarinoapplocal`) e introspección con
   `information_schema`, `pg_proc`, `pg_trigger`, `pg_get_functiondef/viewdef/triggerdef`.
2. **Cruce** del estado real contra entidades (`Domain/Entities`), configuraciones EF
   (`Infrastructure/Persistence/Configurations`) y el contenido de las migraciones.
3. **Detección de objetos sin migración** y creación de una migración idempotente que los
   registre, con DDL tomado del estado real de la BD (no de scripts `.sql` históricos).
4. **Validación local**: la migración aplica sin error y los objetos persisten.

## Resultado

- **Migración creada:** `20260531180558_AddMissingDbFunctionsTriggersAndViews`
  → registra **13 funciones**, **6 triggers** y **3 vistas** que solo existían por scripts manuales.
- **Validada localmente:** `dotnet ef database update` OK; 16 funciones / 7 triggers / 3 vistas
  presentes; smoke tests de funciones OK.
- **Notas / desalineaciones** (detalle en Parte B §6):
  - ℹ️ Separación de seguimiento por país (Ecuador/Panamá) **descartada**: tablas dropeadas a
    propósito. Decisión: **solo documentar, no tocar código** (quedan controllers/servicios
    cableados que fallarían si se invocan; ver Parte B §6.1).
  - 🟡 Tablas huérfanas `user_paises`, `guia_semana` (sin entidad).
  - 🟡 FKs duplicadas en `granja_id`/`nucleo_id`.

## Siguiente paso (despliegue)

Hacer commit de la migración + docs y desplegar por el flujo normal (ECS aplica migraciones al
arrancar). La migración nueva solo crea funciones/triggers/vistas faltantes (idempotente), no
toca el tema Ecuador/Panamá.
