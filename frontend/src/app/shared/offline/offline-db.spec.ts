import { STORE_CONSULTAS, STORE_OUTBOX, aplicarMigraciones } from './offline-db';

/**
 * El test que el plan madre exige explícitamente (§5.2): abrir una base en v1, llevarla a vN de un
 * salto, y verificar que corrieron **todos** los pasos intermedios.
 *
 * IndexedDB entrega **un solo** `upgradeneeded` de v1 a v5. El patrón intuitivo —un `if/else if`
 * sobre `oldVersion`, o un `switch` sin fallthrough— ejecuta un solo paso y se saltea los demás en
 * silencio. El dispositivo que hace ese salto es justo el que estuvo meses sin abrir la app, o sea
 * el que nadie tiene a mano para depurar.
 */
describe('aplicarMigraciones (IndexedDB)', () => {
  const dbFalsa = {} as IDBDatabase;

  it('🔴 un salto de v1 a v3 corre los pasos 2 Y 3, no solo uno', () => {
    const corridos: number[] = [];
    const pasos = {
      1: () => corridos.push(1),
      2: () => corridos.push(2),
      3: () => corridos.push(3)
    };

    const aplicados = aplicarMigraciones(dbFalsa, 1, 3, pasos);

    expect(corridos).toEqual([2, 3]);
    expect(aplicados).toEqual([2, 3]);
  });

  it('una base nueva (oldVersion 0) corre TODOS los pasos desde el 1', () => {
    const corridos: number[] = [];
    const pasos = {
      1: () => corridos.push(1),
      2: () => corridos.push(2),
      3: () => corridos.push(3)
    };

    aplicarMigraciones(dbFalsa, 0, 3, pasos);

    expect(corridos).toEqual([1, 2, 3]);
  });

  it('los pasos corren EN ORDEN', () => {
    const corridos: number[] = [];
    const pasos = {
      1: () => corridos.push(1),
      2: () => corridos.push(2),
      3: () => corridos.push(3),
      4: () => corridos.push(4)
    };

    aplicarMigraciones(dbFalsa, 0, 4, pasos);

    expect(corridos).toEqual([1, 2, 3, 4]);
  });

  it('una base ya al día no corre nada', () => {
    const corridos: number[] = [];
    const pasos = { 1: () => corridos.push(1) };

    expect(aplicarMigraciones(dbFalsa, 1, 1, pasos)).toEqual([]);
    expect(corridos).toEqual([]);
  });

  it('una versión sin paso definido no rompe la cadena', () => {
    const corridos: number[] = [];
    const pasos = {
      1: () => corridos.push(1),
      3: () => corridos.push(3) // la v2 no tuvo cambios de esquema
    };

    aplicarMigraciones(dbFalsa, 0, 3, pasos);

    expect(corridos).toEqual([1, 3]);
  });
});

/**
 * Los pasos REALES del esquema (F3 sube la base a v2 para agregar el outbox).
 *
 * Con una base de verdad, no con un doble: lo que puede salir mal es que el paso 2 se olvide de
 * crear un índice, o que un dispositivo que venía en v1 pierda lo que ya tenía guardado. Ninguna de
 * las dos cosas se ve con un objeto falso.
 */
describe('esquema real v1 → v2 (IndexedDB de verdad)', () => {
  const NOMBRE = `italgranja-migracion-${Date.now()}`;

  afterAll(async () => {
    await new Promise<void>(resolve => {
      const req = indexedDB.deleteDatabase(NOMBRE);
      req.onsuccess = () => resolve();
      req.onerror = () => resolve();
      req.onblocked = () => resolve();
    });
  });

  it('🔴 al subir de v1 a v2 se crea el outbox y NO se pierde lo guardado en consultas', async () => {
    // v1: solo `consultas`, con una entrada dentro.
    const v1 = await abrir(NOMBRE, 1, db => {
      const store = db.createObjectStore(STORE_CONSULTAS, { keyPath: 'clave' });
      store.createIndex('por_particion', 'particion', { unique: false });
      store.createIndex('por_fecha', 'guardadoEn', { unique: false });
    });
    await escribir(v1, STORE_CONSULTAS, { clave: 'k1', particion: 'p1', cuerpo: { a: 1 }, guardadoEn: 1, url: '/x' });
    v1.close();

    // v2: con los pasos reales del esquema.
    const v2 = await abrir(NOMBRE, 2, (db, oldVersion, newVersion) =>
      aplicarMigraciones(db, oldVersion, newVersion)
    );

    expect(Array.from(v2.objectStoreNames)).toContain(STORE_OUTBOX);

    const outbox = v2.transaction(STORE_OUTBOX, 'readonly').objectStore(STORE_OUTBOX);
    expect(Array.from(outbox.indexNames).sort()).toEqual(['por_estado', 'por_particion']);
    expect(outbox.keyPath).toBe('clientOpId');

    // Lo que el dispositivo ya tenía sigue ahí: una migración que vacía la caché obligaría a bajar
    // todo de nuevo justo cuando el equipo puede no tener red.
    const guardado = await leer(v2, STORE_CONSULTAS, 'k1');
    expect(guardado).toBeTruthy();

    v2.close();
  });

  function abrir(
    nombre: string,
    version: number,
    upgrade: (db: IDBDatabase, oldVersion: number, newVersion: number) => void
  ): Promise<IDBDatabase> {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(nombre, version);
      req.onupgradeneeded = ev => upgrade(req.result, ev.oldVersion, ev.newVersion ?? version);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  }

  function escribir(db: IDBDatabase, store: string, valor: unknown): Promise<void> {
    return new Promise((resolve, reject) => {
      const tx = db.transaction(store, 'readwrite');
      tx.objectStore(store).put(valor);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  }

  function leer(db: IDBDatabase, store: string, clave: string): Promise<unknown> {
    return new Promise((resolve, reject) => {
      const req = db.transaction(store, 'readonly').objectStore(store).get(clave);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  }
});
