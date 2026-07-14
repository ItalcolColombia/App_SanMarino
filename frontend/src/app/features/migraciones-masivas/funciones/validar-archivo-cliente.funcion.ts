// features/migraciones-masivas/funciones/validar-archivo-cliente.funcion.ts
/**
 * Validación de archivo en el cliente, antes de habilitar Validar/Importar: evita una ida y vuelta
 * al servidor por un archivo con extensión incorrecta, vacío o demasiado pesado. Función pura.
 */
const EXTENSION_VALIDA = '.xlsx';

/** Devuelve el mensaje de error a mostrar, o `null` si el archivo es aceptable. */
export function validarArchivoCliente(file: File, maxBytes = 10 * 1024 * 1024): string | null {
  const nombre = (file.name || '').toLowerCase();
  if (!nombre.endsWith(EXTENSION_VALIDA)) {
    return 'El archivo debe tener extensión .xlsx.';
  }
  if (file.size <= 0) {
    return 'El archivo está vacío.';
  }
  if (file.size > maxBytes) {
    const maxMb = Math.round(maxBytes / (1024 * 1024));
    return `El archivo supera el tamaño máximo permitido (${maxMb} MB).`;
  }
  return null;
}
