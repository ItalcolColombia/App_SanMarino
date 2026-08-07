// src/app/features/italjira/funciones/armar-arbol-backlog.funcion.ts
// Función PURA: sin `this`, sin DI, sin HTTP. Ver funciones/README.md.

import type { TicketTarea } from '../../tickets/models/ticket-tarea.models';

/** Una tarea de primer nivel con las subtareas/bugs que cuelgan de ella. */
export interface NodoTarea {
  tarea: TicketTarea;
  hijos: TicketTarea[];
}

/**
 * Convierte la lista PLANA que devuelve el backend en el árbol de dos niveles que pinta el backlog:
 * tarea → subtareas/bugs.
 *
 * Una subtarea cuyo padre no está en la lista (porque se filtró, o porque el padre se borró
 * lógicamente) se promueve a primer nivel en vez de desaparecer: perder trabajo de la pantalla es
 * peor que mostrarlo un nivel más arriba.
 */
export function armarArbolTareas(tareas: readonly TicketTarea[]): NodoTarea[] {
  const ids = new Set(tareas.map(t => t.id));

  const raices = tareas.filter(t => t.parentTareaId === null || !ids.has(t.parentTareaId));
  const hijosPorPadre = new Map<number, TicketTarea[]>();

  for (const t of tareas) {
    if (t.parentTareaId === null || !ids.has(t.parentTareaId)) continue;
    const lista = hijosPorPadre.get(t.parentTareaId);
    if (lista) lista.push(t);
    else hijosPorPadre.set(t.parentTareaId, [t]);
  }

  return raices.map(tarea => ({
    tarea,
    hijos: hijosPorPadre.get(tarea.id) ?? [],
  }));
}

/** Totales de una historia calculados sobre las tareas que efectivamente se están mostrando. */
export interface TotalesArbol {
  total: number;
  listas: number;
  horasRegistradas: number;
  horasEstimadas: number | null;
}

/**
 * Suma el árbol completo (raíces + subtareas). Cuenta cada tarea UNA vez: las subtareas ya vienen
 * en la misma lista plana, así que sumar el árbol sería contarlas dos veces.
 */
export function totalesDeTareas(tareas: readonly TicketTarea[]): TotalesArbol {
  let listas = 0;
  let horas = 0;
  let estimadas = 0;
  let hayEstimadas = false;

  for (const t of tareas) {
    if (t.estado === 'LISTO') listas++;
    horas += t.horasRegistradas ?? 0;
    if (t.horasEstimadas !== null && t.horasEstimadas !== undefined) {
      estimadas += t.horasEstimadas;
      hayEstimadas = true;
    }
  }

  return {
    total: tareas.length,
    listas,
    horasRegistradas: horas,
    horasEstimadas: hayEstimadas ? estimadas : null,
  };
}
