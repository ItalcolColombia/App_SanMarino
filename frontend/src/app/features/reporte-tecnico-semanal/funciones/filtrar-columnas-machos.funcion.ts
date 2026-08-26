/**
 * Filtro de columnas de machos del reporte técnico semanal — empresas con
 * `Company.ocultaMachosEnPostura` (SR-DEF-1).
 *
 * Función **PURA** (sin `this`, sin DI, sin estado). Se aplica sobre los arrays `COLUMNAS_*` de
 * `columnas-reporte-semanal.funcion.ts` y `columnas-resumen-ra-pesadas.funcion.ts`, que son la
 * MISMA fuente para la cabecera, las celdas y el Excel: filtrando ahí, los tres quedan
 * consistentes por construcción y no hay forma de que la tabla se corra.
 *
 * <p>Con el flag apagado devuelve **el mismo array**, no una copia: las referencias se mantienen
 * estables entre ciclos de change detection, como exige CLAUDE.md para lo que consume el template.</p>
 */

/** Prefijo de los grupos de machos: `M · Mortalidad`, `M · Peso`, `M · Alimento`… */
const PREFIJO_GRUPO_MACHOS = 'M · ';

/**
 * Grupos que se retiran completos. `Apareo M:H %` y los de `Error sexaje` **de los dos sexos**
 * dejan de tener sentido sin machos: el apareo es una relación macho/hembra y el error de sexaje
 * desaparece como concepto (misma decisión que en los formularios, F5.1/F5.2).
 */
const GRUPOS_EXCLUIDOS = new Set<string>([
  'Machos',
  'Apareo M:H %',
  'H · Error sexaje',
  'M · Error sexaje'
]);

/** Columnas sueltas dentro de un grupo mixto (p. ej. `Aves › Machos`, `Venta aves › Machos`). */
const TITULOS_EXCLUIDOS = new Set<string>([
  'Machos',
  'M:H %',
  'Saldo M',
  'M % Guía'
]);

/** Una columna cualquiera de los reportes semanales: solo se mira su grupo y su título. */
interface ColumnaConGrupo {
  grupo: string;
  titulo: string;
}

/** ¿Esta columna es de machos (o de un concepto que muere con ellos)? */
export function esColumnaDeMachos(columna: ColumnaConGrupo): boolean {
  return columna.grupo.startsWith(PREFIJO_GRUPO_MACHOS)
    || GRUPOS_EXCLUIDOS.has(columna.grupo)
    || TITULOS_EXCLUIDOS.has(columna.titulo);
}

/**
 * Devuelve las columnas sin las de machos. Con `ocultaMachos` en `false` devuelve el array
 * recibido tal cual (misma referencia).
 */
export function filtrarColumnasMachos<T extends ColumnaConGrupo>(
  columnas: readonly T[],
  ocultaMachos: boolean
): readonly T[] {
  return ocultaMachos ? columnas.filter(c => !esColumnaDeMachos(c)) : columnas;
}
