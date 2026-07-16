# Estado del proyecto (generado por devpilot)

**Misión:** Matriz Verenice rev 6-jul-26 — Postura Colombia (validación + corrección)
**Estado:** planned (validación completada; implementación no iniciada)
**Progreso:** Fase de validación 4/5 (falta validación en vivo — credenciales pendientes)
**Actualizado:** 2026-07-16

## Hecho
- [x] Lectura completa del Excel rev 6jul26 (12 hojas + 17 screenshots embebidos)
- [x] Investigación de código en main por REQ (8 agentes, evidencia file:line)
- [x] Verificación de datos en BD local (lotes 116/117 encaset futuro, filas traslado fantasma, K345 año 2023)
- [x] Plan `fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md` + tracker reescrito

## Pendiente (fases del plan)
- [ ] Fase 0 — Data-fix `backend/sql/fix_datos_postura_verenice_jul26.sql` (local ya; prod con OK)
- [ ] Fase 1 — Hotfixes front (columnas corridas REQ-002m/n, REQ-008b, REQ-012e, REQ-001a)
- [ ] Fase 2 — fn_indicadores_levante_postura (H/M, acumulados, defensas) + migración
- [ ] Fase 3 — Semana/edad/etapa + validaciones (REQ-011, REQ-012, REQ-007d, REQ-009c)
- [ ] Fase 4 — Traslado (default fecha local/último registro)
- [ ] Fase 5 — Reporte semana por sexo + barrido conversión alimenticia + selector H/M
- [ ] Fase 6 — Transversales (REQ-005 migración vista, REQ-006 enforcement, REQ-003, REQ-004, REQ-000a/b/c)
- [ ] Fase 7 — Fórmulas nuevas (bloqueadas por insumos de la líder: tabla nutricional, % huevo guía, fórmula IP, embriodiagnosis)

## Misión anterior (cerrada)
Refactor de archivos largos front+back: 13/14 tareas completadas (quedó pendiente solo la actualización de tracker/knowledge, cubierta ahora).
