// src/app/features/config/guia-genetica-santa-reyes/funciones/resumir-import.funcion.ts
/**
 * Traduce el resultado del import a lo que el usuario lee. Función **pura**: sin `this`, sin DI,
 * sin toast (el componente decide si lo muestra como `success`, `warning` o `error`).
 */
import { GuiaGeneticaSantaReyesImportResultDto } from '../models/guia-genetica-santa-reyes.model';

/** Tono del mensaje, en los términos del `ToastService`. */
export type TonoImport = 'success' | 'warning' | 'error';

/** Resumen legible del import. */
export interface ResumenImportGuia {
  tono: TonoImport;
  /** Una línea para el toast. */
  mensaje: string;
  /** Detalle para el modal (una línea por métrica). */
  detalle: string[];
  /** `true` si hubo filas rechazadas y conviene dejar el modal abierto para que se vean. */
  hayErrores: boolean;
}

/**
 * Resume el import.
 *
 * 🔴 **Un import parcial NO se anuncia como éxito.** El backend devuelve `success: false` cuando
 * alguna fila quedó afuera, con las buenas ya aplicadas: decir «listo» ahí escondería tres filas
 * perdidas. Por eso hay un tono `warning` propio para «entró casi todo».
 *
 * 🔴 **`omitidos` no es un fracaso**: reimportar el mismo archivo da `insertados = 0` y todo en
 * `omitidos` porque el import es idempotente y una fila idéntica no se reescribe. Ese caso se
 * cuenta como éxito y el mensaje lo dice con todas las letras, para que nadie lo lea como «no hizo
 * nada, está roto».
 */
export function resumirImportGuia(
  resultado: GuiaGeneticaSantaReyesImportResultDto | null | undefined
): ResumenImportGuia {
  if (!resultado) {
    return {
      tono: 'error',
      mensaje: 'El servidor no devolvió el resultado del import.',
      detalle: [],
      hayErrores: true
    };
  }

  const { insertados, actualizados, omitidos, totalFilas } = resultado;
  const errores = resultado.errores?.length ?? 0;
  const cambios = insertados + actualizados;

  const detalle = [
    `Filas leídas: ${totalFilas}`,
    `Líneas nuevas: ${insertados}`,
    `Líneas actualizadas: ${actualizados}`,
    `Sin cambios: ${omitidos}`,
    `Filas rechazadas: ${errores}`
  ];

  if (errores > 0) {
    // Con cero cambios y errores, no entró nada: eso es un fallo, no una advertencia.
    const tono: TonoImport = cambios > 0 ? 'warning' : 'error';
    const mensaje = cambios > 0
      ? `Import parcial: ${cambios} línea(s) aplicada(s) y ${errores} fila(s) rechazada(s).`
      : `No se aplicó ninguna línea: ${errores} fila(s) rechazada(s).`;
    return { tono, mensaje, detalle, hayErrores: true };
  }

  if (cambios === 0) {
    return {
      tono: 'success',
      mensaje: 'El archivo ya estaba cargado: no hubo cambios (el import es idempotente).',
      detalle,
      hayErrores: false
    };
  }

  return {
    tono: 'success',
    mensaje: `Import correcto: ${insertados} línea(s) nueva(s) y ${actualizados} actualizada(s).`,
    detalle,
    hayErrores: false
  };
}

/** Extensiones que acepta el backend, y el tope de tamaño (10 MB), para validar antes de subir. */
export const EXTENSIONES_IMPORT_GUIA = ['.xlsx', '.xls'];

/** @see EXTENSIONES_IMPORT_GUIA */
export const MAX_BYTES_IMPORT_GUIA = 10 * 1024 * 1024;

/**
 * Chequeo local del archivo antes de gastar la subida. Devuelve el motivo del rechazo o `null`.
 * Espeja `ValidarArchivo` del service; el backend vuelve a validarlo igual.
 */
export function validarArchivoImportGuia(file: File | null | undefined): string | null {
  if (!file) return 'Debe seleccionar un archivo Excel (.xlsx / .xls).';
  if (file.size <= 0) return 'El archivo está vacío.';
  if (file.size > MAX_BYTES_IMPORT_GUIA) {
    return `El archivo es demasiado grande. Tamaño máximo permitido: ${MAX_BYTES_IMPORT_GUIA / (1024 * 1024)} MB.`;
  }

  const punto = file.name.lastIndexOf('.');
  const extension = punto >= 0 ? file.name.slice(punto).toLowerCase() : '';
  if (!EXTENSIONES_IMPORT_GUIA.includes(extension)) {
    return `Formato de archivo no válido. Se permiten: ${EXTENSIONES_IMPORT_GUIA.join(', ')}.`;
  }

  return null;
}
