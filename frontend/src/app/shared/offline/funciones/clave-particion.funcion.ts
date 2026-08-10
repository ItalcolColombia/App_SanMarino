import type { IdentidadParticion } from '../models/offline.model';

/**
 * Construye la partición de la caché offline: `{userId}|{companyId}|{paisId}`.
 *
 * ## Por qué es FAIL-CLOSED
 *
 * Es la función que impide la fuga de datos entre empresas, y por eso devuelve `null` —en vez de
 * una clave parcial— apenas falta uno de los tres identificadores. Degradar a `"|5|1"` cuando no hay
 * `userId` parece inofensivo y es justamente el mecanismo de la fuga: dos sesiones distintas
 * colapsan en la misma clave y la segunda lee lo que guardó la primera.
 *
 * `null` significa **no cachear y no leer caché**. Perder la consulta offline de una pantalla es un
 * costo aceptable; mostrarle a un operario los datos de otra empresa no lo es.
 *
 * Es el mismo criterio que aplica el backend en `InventarioCatalogoScopeCalculos`: ante ambigüedad
 * de alcance, vacío antes que datos de más.
 */
export function claveParticion(identidad: IdentidadParticion | null | undefined): string | null {
  if (!identidad) {
    return null;
  }

  const { userId, companyId, paisId } = identidad;

  // `0` no es un id válido en ninguna de las tres dimensiones, y `''` tampoco: se tratan como
  // ausencia. Un `?? ` o un chequeo de `!= null` dejaría pasar el 0 y volvería a colapsar claves.
  const user = normalizar(userId);
  const company = normalizar(companyId);
  const pais = normalizar(paisId);

  if (user === null || company === null || pais === null) {
    return null;
  }

  return `${user}|${company}|${pais}`;
}

/**
 * Clave completa de una entrada: partición + método + URL.
 *
 * El método entra en la clave porque un día podría cachearse algo que no sea GET, y una clave que
 * no lo distingue haría que esa respuesta pisara la del GET de la misma URL.
 */
export function claveEntrada(particion: string, metodo: string, url: string): string {
  return `${particion}|${metodo.toUpperCase()} ${url}`;
}

function normalizar(valor: string | number | null | undefined): string | null {
  if (valor === null || valor === undefined) {
    return null;
  }
  const texto = String(valor).trim();
  if (texto === '' || texto === '0') {
    return null;
  }
  return texto;
}
