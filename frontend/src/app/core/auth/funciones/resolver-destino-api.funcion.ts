export interface DestinoApi {
  esApi: boolean;
  esLogin: boolean;
}

/**
 * Resuelve las URLs como el navegador antes de decidir si pueden recibir credenciales.
 * El origen y el límite de segmento importan: `/api-externa` no pertenece a `/api`.
 * Se rechazan userinfo y separadores codificados para evitar interpretaciones distintas
 * entre el navegador, un proxy y el servidor. `baseUri` es el document.baseURI real.
 */
export function resolverDestinoApi(url: string, apiUrl: string, baseUri: string): DestinoApi {
  const externo: DestinoApi = { esApi: false, esLogin: false };
  if (!url.trim() || !apiUrl.trim()) return externo;

  try {
    const api = new URL(apiUrl, baseUri);
    const destino = new URL(url, baseUri);
    if (!['http:', 'https:'].includes(api.protocol) || destino.origin !== api.origin) return externo;
    if (api.username || api.password || destino.username || destino.password) return externo;
    if (/%(?:2f|5c|25)/i.test(destino.pathname) || /%(?:2f|5c|25)/i.test(api.pathname)) return externo;

    const prefijo = api.pathname.replace(/\/+$/, '');
    const ruta = destino.pathname;
    if (ruta !== prefijo && !ruta.startsWith(`${prefijo}/`)) return externo;

    return {
      esApi: true,
      esLogin: /^\/auth\/login\/?$/i.test(ruta.slice(prefijo.length))
    };
  } catch {
    return externo;
  }
}
