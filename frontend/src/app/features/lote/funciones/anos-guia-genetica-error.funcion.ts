// src/app/features/lote/funciones/anos-guia-genetica-error.funcion.ts

/**
 * Mensaje para el campo «Año Tabla Genética» cuando la consulta de años **falla**.
 *
 * <p>
 * 🔴 **Por qué existe.** El `error:` de `loadAnosDisponibles` colapsaba dos casos distintos en un
 * solo estado (`razaValida = false`): «la guía no tiene años para esta raza» y «la request no
 * respondió». Los dos pintaban el mismo texto rojo, así que un fallo de sesión se leía como un
 * problema de datos. Medido en una capacitación de Sanmarino (9-sep-2026): venció la sesión,
 * `GET /guia-genetica/info-raza` no respondió, y el formulario dijo *«No se encontraron años
 * disponibles para la raza C500»* — con los 4 años de esa raza (2021/2022/2023/2026) intactos en
 * la guía. **Un fallo de red o de sesión no es un dato faltante**, y confundirlos manda a buscar
 * el problema en la guía genética.
 * </p>
 *
 * <p>
 * `GuiaGeneticaService.handleError` ya traduce el status HTTP a un mensaje accionable
 * («No autorizado. Inicie sesión nuevamente» para el 401, etc.) y lo emite como `Error.message`;
 * hasta ahora el componente lo descartaba en un `console.error`. Acá solo se le antepone el
 * contexto, para que quede claro que lo que falló fue **la consulta**, no la raza elegida.
 * </p>
 *
 * Función pura: sin `this`, sin DI, sin estado.
 */
export function mensajeErrorAnosGuiaGenetica(error: unknown): string {
  const detalle = (error as { message?: unknown } | null | undefined)?.message;
  const texto = typeof detalle === 'string' ? detalle.trim() : '';

  return texto
    ? `No se pudo consultar la guía genética: ${texto}`
    : 'No se pudo consultar la guía genética. Reintente; si el problema persiste, vuelva a iniciar sesión.';
}
