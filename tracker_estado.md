# Tracker de Estado — Migración Lesiones → Seguimiento Diario

IMPORTANTE: Antes de ejecutar cambios, leer y seguir `CLAUDE.md` (proceso obligatorio).

Plan: [fase_de_desarrollo/16_migracion_lesiones_plan.md](fase_de_desarrollo/16_migracion_lesiones_plan.md)
Fecha: 2026-05-27
Estado: En espera — listo para iniciar fase 1

## Checklist (ejecución incremental)

- [ ] 1) Crear rama `feat/lesiones-move-seguimientodiario`
- [ ] 2) Remover sección de Lesiones en `lote-reproductora-ave-engorde-list.component.html`
- [ ] 3) Añadir botón `+ Registrar Lesión` en cabecera de `seguimiento-diario` module
- [ ] 4) Mover/Exportar componente Lesiones al módulo `seguimiento-diario`
- [ ] 5) Implementar Tabs (Seguimiento Diario / Histórico de Lesiones)
- [ ] 6) Asegurar reactividad: Lesiones escucha filtros y refresca al crear/editar/eliminar
- [ ] 7) Verificar endpoints backend filtran por `loteReproductorId`
- [ ] 8) QA: pruebas manuales y aprobación visual

## Referencias
- Plan técnico: `fase_de_desarrollo/16_migracion_lesiones_plan.md`
- Requisitos operativos y workflow obligatorios: `CLAUDE.md`

Estado actual: pendiente — confirmar para iniciar la Fase 1 (eliminación en módulo origen).
