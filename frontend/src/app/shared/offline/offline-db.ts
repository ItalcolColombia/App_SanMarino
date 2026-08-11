import type { EntradaCache } from './models/offline.model';

export const NOMBRE_BD = 'italgranja-offline';
export const VERSION_BD = 1;
export const STORE_CONSULTAS = 'consultas';

/**
 * Pasos de migración del esquema, **uno por versión**.
 *
 * ## La trampa que esto evita
 *
 * IndexedDB entrega **un solo** evento `upgradeneeded` al saltar de v1 a v5, con `oldVersion = 1` y
 * `newVersion = 5`. El patrón intuitivo —un `if (oldVersion < 2) {...} else if (oldVersion < 3)`, o
 * un `switch` sin fallthrough— ejecuta **un solo paso** y se saltea los demás en silencio: el
 * esquema queda a medias y los errores aparecen mucho después, en un dispositivo que nadie tiene a
 * mano. Un dispositivo que estuvo meses sin abrir la app es exactamente el que hace ese salto.
 *
 * Por eso los pasos viven en un mapa indexado por versión y el handler itera **todas** las versiones
 * intermedias. Agregar una versión nueva es agregar una entrada acá y subir `VERSION_BD`.
 */
export const PASOS_MIGRACION: Record<number, (db: IDBDatabase) => void> = {
  1: db => {
    const store = db.createObjectStore(STORE_CONSULTAS, { keyPath: 'clave' });
    // Índice por partición: permite purgar toda la empresa/usuario de un saque en logout o
    // cambio de empresa, sin recorrer la base entera.
    store.createIndex('por_particion', 'particion', { unique: false });
    store.createIndex('por_fecha', 'guardadoEn', { unique: false });
  }
};

/**
 * Aplica todos los pasos entre `oldVersion` y `newVersion`, en orden.
 *
 * Se exporta aparte del `open()` para poder testearlo sin abrir una base real.
 */
export function aplicarMigraciones(
  db: IDBDatabase,
  oldVersion: number,
  newVersion: number,
  pasos: Record<number, (db: IDBDatabase) => void> = PASOS_MIGRACION
): number[] {
  const aplicados: number[] = [];

  for (let v = oldVersion + 1; v <= newVersion; v++) {
    const paso = pasos[v];
    if (paso) {
      paso(db);
      aplicados.push(v);
    }
  }

  return aplicados;
}

/** Abre (y migra) la base. Devuelve `null` si el navegador no soporta IndexedDB o la bloquea. */
export function abrirBd(nombre = NOMBRE_BD, version = VERSION_BD): Promise<IDBDatabase | null> {
  return new Promise(resolve => {
    if (typeof indexedDB === 'undefined') {
      resolve(null);
      return;
    }

    let req: IDBOpenDBRequest;
    try {
      req = indexedDB.open(nombre, version);
    } catch {
      // Modo incógnito de algunos navegadores, o almacenamiento deshabilitado por política.
      resolve(null);
      return;
    }

    req.onupgradeneeded = evento => {
      aplicarMigraciones(req.result, evento.oldVersion, evento.newVersion ?? version);
    };
    req.onsuccess = () => resolve(req.result);
    // Fallar abriendo la caché nunca puede romper la app: se sigue sin caché.
    req.onerror = () => resolve(null);
    req.onblocked = () => resolve(null);
  });
}

/** Envuelve un `IDBRequest` en promesa. Los errores se resuelven a `null`, nunca rechazan. */
function pedir<T>(req: IDBRequest<T>): Promise<T | null> {
  return new Promise(resolve => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => resolve(null);
  });
}

export async function leerEntrada(db: IDBDatabase, clave: string): Promise<EntradaCache | null> {
  const tx = db.transaction(STORE_CONSULTAS, 'readonly');
  const res = await pedir<EntradaCache>(tx.objectStore(STORE_CONSULTAS).get(clave) as IDBRequest<EntradaCache>);
  return res ?? null;
}

export async function guardarEntrada(db: IDBDatabase, entrada: EntradaCache): Promise<void> {
  const tx = db.transaction(STORE_CONSULTAS, 'readwrite');
  await pedir(tx.objectStore(STORE_CONSULTAS).put(entrada));
}

/**
 * Borra todas las entradas de una partición.
 * Se usa en logout y al cambiar de empresa: no se espera al TTL.
 */
export async function purgarParticion(db: IDBDatabase, particion: string): Promise<number> {
  const tx = db.transaction(STORE_CONSULTAS, 'readwrite');
  const indice = tx.objectStore(STORE_CONSULTAS).index('por_particion');
  const claves = await pedir<IDBValidKey[]>(indice.getAllKeys(IDBKeyRange.only(particion)));

  if (!claves?.length) {
    return 0;
  }

  const txBorrado = db.transaction(STORE_CONSULTAS, 'readwrite');
  const store = txBorrado.objectStore(STORE_CONSULTAS);
  for (const clave of claves) {
    store.delete(clave);
  }

  return claves.length;
}

/** Borra TODA la caché. Se usa en logout: el dispositivo puede cambiar de manos. */
export async function purgarTodo(db: IDBDatabase): Promise<void> {
  const tx = db.transaction(STORE_CONSULTAS, 'readwrite');
  await pedir(tx.objectStore(STORE_CONSULTAS).clear());
}

export async function contarEntradas(db: IDBDatabase): Promise<number> {
  const tx = db.transaction(STORE_CONSULTAS, 'readonly');
  return (await pedir<number>(tx.objectStore(STORE_CONSULTAS).count())) ?? 0;
}

export async function leerTodas(db: IDBDatabase): Promise<EntradaCache[]> {
  const tx = db.transaction(STORE_CONSULTAS, 'readonly');
  return (await pedir<EntradaCache[]>(tx.objectStore(STORE_CONSULTAS).getAll() as IDBRequest<EntradaCache[]>)) ?? [];
}
