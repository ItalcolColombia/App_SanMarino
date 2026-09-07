/**
 * Decide QUÉ valor mandar en el header `X-Secret-Up` y si hay que cifrarlo.
 *
 * - `'derivada'`: la sesión trae `platformKey` (firma por sesión que emitió el backend, derivada del
 *   `jti`). Se manda tal cual, sin cifrar — ya es opaca y sólo sirve para esa sesión.
 * - `'legacy'`: no hay `platformKey` (sesión anterior al cambio, o backend sin `DerivationKey`). Se
 *   cae al secreto estático del bundle, que el interceptor todavía tiene que cifrar antes de mandar.
 *
 * Pura: sin `this`, sin DI, sin servicios. Toda la asincronía (cifrado del legacy) la maneja el
 * interceptor según el `modo` que devuelve esta función.
 */
export type ModoFirmaPlataforma = 'derivada' | 'legacy';

export interface FirmaPlataforma {
  modo: ModoFirmaPlataforma;
  /** Valor a mandar (derivada) o a cifrar (legacy). `null` si no hay ninguno disponible. */
  valor: string | null;
}

export function resolverFirmaPlataforma(
  session: { platformKey?: string | null } | null | undefined,
  secretoEstatico: string | null | undefined
): FirmaPlataforma {
  const platformKey = session?.platformKey;
  if (typeof platformKey === 'string' && platformKey.trim().length > 0) {
    return { modo: 'derivada', valor: platformKey };
  }
  return { modo: 'legacy', valor: secretoEstatico ?? null };
}
