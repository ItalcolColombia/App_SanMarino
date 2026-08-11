import type { AuthSession } from '../../../core/auth/auth.models';

/**
 * ¿Esta cuenta puede guardar consultas para verlas sin conexión?
 *
 * Implementa la parte **dura** de la decisión D6 del plan de PWA: *prohibido para cuentas con
 * alcance global o multiempresa*. Es una regla de protección de datos, no una preferencia.
 *
 * ## Por qué la partición de la caché no alcanza
 *
 * `claveParticion` impide que una sesión **lea** lo que guardó otra, y eso funciona. Pero no impide
 * que **el mismo dispositivo acumule** los datos de todas las empresas que ese usuario visita, cada
 * una prolijamente en su partición. Como se decidió **no cifrar** el dato en reposo (D3), un
 * dispositivo perdido expone lo de todas.
 *
 * Son dos amenazas distintas: la partición cubre la fuga *entre sesiones*, esto cubre la pérdida del
 * equipo. Un super admin con la app instalada terminaría con el snapshot de la operación completa en
 * una tablet de granja.
 *
 * ## Criterio conservador ante señales que no coinciden
 *
 * "Tiene más de una empresa" se evalúa por las tres señales que trae la sesión, y **basta que una lo
 * diga**. No se exige que coincidan: las llena el backend por caminos distintos y pueden desfasarse.
 * Cuando la duda es "¿bajo datos de más?", la respuesta segura es no bajarlos.
 */
export function decidirCacheOffline(sesion: AuthSession | null | undefined): boolean {
  if (!sesion) {
    // Fail-closed, mismo criterio que `claveParticion`: sin identidad no se guarda ni se lee.
    return false;
  }

  if (sesion.user?.isSuperAdmin === true) {
    return false;
  }

  return !tieneVariasEmpresas(sesion);
}

/**
 * ¿La cuenta alcanza más de una empresa? Mira las tres señales disponibles y devuelve `true` si
 * **cualquiera** de ellas lo indica.
 */
function tieneVariasEmpresas(sesion: AuthSession): boolean {
  if (sesion.user?.hasMultipleCompanies === true) {
    return true;
  }

  // Se cuentan valores DISTINTOS y no nulos: un arreglo con el mismo id repetido, o con huecos,
  // describe una sola empresa y no debería bloquear a un operario normal.
  return contarDistintos(sesion.companyIds) > 1 || contarDistintos(sesion.companies) > 1;
}

function contarDistintos(valores: ReadonlyArray<string | number | null | undefined> | null | undefined): number {
  if (!Array.isArray(valores)) {
    return 0;
  }

  const vistos = new Set<string>();
  for (const valor of valores) {
    if (valor === null || valor === undefined) {
      continue;
    }
    const texto = String(valor).trim();
    // El `0` y la cadena vacía no son empresas: son el hueco que deja un campo sin llenar.
    if (texto === '' || texto === '0') {
      continue;
    }
    vistos.add(texto);
  }
  return vistos.size;
}
