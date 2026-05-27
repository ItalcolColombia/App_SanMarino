# Tracker de Estado — Migración Lesiones → Seguimiento Diario

IMPORTANTE: Antes de ejecutar cambios, leer y seguir `CLAUDE.md` (proceso obligatorio).

Plan: [fase_de_desarrollo/16_migracion_lesiones_plan.md](fase_de_desarrollo/16_migracion_lesiones_plan.md)
Fecha: 2026-05-27
Estado: En espera — listo para iniciar fase 1

## Checklist (ejecución incremental)

- [ ] 1) Remover sección de Lesiones en `Lote Reproductora Aves de Engorde` (`lote-reproductora-ave-engorde-list.component.html`)
- [ ] 2) Añadir botón `+ Registrar Lesión` en cabecera de `Seguimiento diario reproductora aves de engorde`
- [ ] 3) Mover/Exportar componente Lesiones al módulo `Seguimiento diario reproductora aves de engorde`
- [ ] 4) Implementar Tabs (Seguimiento Diario / Histórico de Lesiones)
- [ ] 5) Asegurar reactividad: Lesiones escucha filtros y refresca al crear/editar/eliminar
- [ ] 6) Verificar endpoints backend filtran por `loteReproductorId`
- [ ] 7) QA: pruebas manuales y aprobación visual

## Referencias
- Plan técnico: `fase_de_desarrollo/16_migracion_lesiones_plan.md`
- Requisitos operativos y workflow obligatorios: `CLAUDE.md`

Estado actual: pendiente — confirmar para iniciar la Fase 1 (eliminación en módulo origen).
