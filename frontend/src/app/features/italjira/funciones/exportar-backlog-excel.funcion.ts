// src/app/features/italjira/funciones/exportar-backlog-excel.funcion.ts
// Función PURA de armado + una sola llamada al helper compartido. Ver funciones/README.md.

import { exportarMultiHojaExcel, type HojaExcel } from '../../../shared/utils/excel/exportar-tabla-excel.funcion';
import { TAREA_ESTADO_LABEL, TAREA_TIPO_LABEL } from '../../tickets/models/ticket-tarea.models';
import type { ItalJiraBacklog } from '../models/historia.models';

/**
 * Arma las tres hojas del Excel del backlog. Es pura: devuelve los datos, no descarga nada — así se
 * puede testear el contenido sin tocar el DOM.
 *
 * Hojas: **Historias** (una fila por épica con su avance y horas), **Tareas** (el trabajo con su
 * historia al lado, incluidas las que todavía no se agruparon) y **Casos sin historia** (la bandeja
 * de entrada: lo que registran los usuarios).
 */
export function construirHojasBacklog(backlog: ItalJiraBacklog): HojaExcel[] {
  const historias: HojaExcel = {
    sheetName: 'Historias',
    title: 'ItalJira — Historias',
    headers: [
      'Código', 'Título', 'Estado', 'Prioridad', 'Responsable',
      'Avance %', 'Terminados', 'Total', 'Horas estimadas', 'Horas registradas',
      'Inicio plan', 'Fin plan', 'Inicio real', 'Fin real', 'Etiquetas',
    ],
    rows: backlog.historias.map(({ historia: h }) => [
      h.codigo, h.titulo, TAREA_ESTADO_LABEL[h.estado] ?? h.estado, h.prioridad,
      h.responsableNombre, h.avancePorcentaje, h.trabajosTerminados, h.trabajosTotales,
      h.horasEstimadas, h.horasRegistradas,
      h.fechaInicioPlan, h.fechaFinPlan, h.fechaInicioReal, h.fechaFinReal, h.etiquetas,
    ]),
  };

  const filasTareas = backlog.historias.flatMap(d =>
    d.tareas.map(t => [
      d.historia.codigo, d.historia.titulo,
      t.codigo, TAREA_TIPO_LABEL[t.tipo] ?? t.tipo, t.titulo,
      TAREA_ESTADO_LABEL[t.estado] ?? t.estado, t.prioridad, t.asignadoNombre,
      t.horasEstimadas, t.horasRegistradas,
      t.fechaInicioPlan, t.fechaFinPlan, t.fechaInicioReal, t.fechaFinReal,
      t.codigoCaso, t.etiquetas,
    ]));

  // Las sueltas también van: si el Excel solo mostrara lo agrupado, parecería que no existen.
  const filasSueltas = backlog.tareasSinHistoria.map(t => [
    null, '(sin historia)',
    t.codigo, TAREA_TIPO_LABEL[t.tipo] ?? t.tipo, t.titulo,
    TAREA_ESTADO_LABEL[t.estado] ?? t.estado, t.prioridad, t.asignadoNombre,
    t.horasEstimadas, t.horasRegistradas,
    t.fechaInicioPlan, t.fechaFinPlan, t.fechaInicioReal, t.fechaFinReal,
    t.codigoCaso, t.etiquetas,
  ]);

  const tareas: HojaExcel = {
    sheetName: 'Tareas',
    title: 'ItalJira — Tareas y subtareas',
    headers: [
      'Historia', 'Historia (título)', 'Código', 'Tipo', 'Título', 'Estado', 'Prioridad',
      'Responsable', 'Horas estimadas', 'Horas registradas',
      'Inicio plan', 'Fin plan', 'Inicio real', 'Fin real', 'Caso', 'Etiquetas',
    ],
    rows: [...filasTareas, ...filasSueltas],
  };

  const casos: HojaExcel = {
    sheetName: 'Casos sin historia',
    title: 'ItalJira — Casos de usuarios todavía sin agrupar',
    headers: [
      'Código', 'Título', 'Tipo', 'Estado', 'Prioridad', 'Responsable',
      'Horas estimadas', 'Horas registradas', 'Tareas', 'Fecha límite', 'Creado',
    ],
    rows: backlog.casosSinHistoria.map(c => [
      c.codigo, c.titulo, c.tipo, c.estado, c.prioridad, c.assignedToNombre,
      c.horasEstimadas, c.horasRegistradas, c.cantidadTareas, c.fechaLimite, c.createdAt,
    ]),
  };

  return [historias, tareas, casos];
}

/** Descarga el Excel del backlog. Un solo punto de salida al helper compartido. */
export function exportarBacklogExcel(backlog: ItalJiraBacklog): void {
  exportarMultiHojaExcel(construirHojasBacklog(backlog), { filenameBase: 'italjira-backlog' });
}
