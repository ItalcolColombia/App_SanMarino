# `shared/offline/funciones/` — reglas puras de la consulta offline

Convención de CLAUDE.md: una función **pura** por responsabilidad, sin `this`, sin DI, sin `HttpClient`
y sin tocar IndexedDB. `cache-consultas.service.ts` y `offline-cache.interceptor.ts` son orquestadores
delgados que las invocan.

## Por qué estas tres están acá

Cada una decide algo cuyo error se paga caro y no se nota:

- **`clave-particion.funcion.ts`** — es lo único que impide que un operario vea los datos de **otra
  empresa**. Es `fail-closed` a propósito: devuelve `null` (⇒ no cachear, no leer) apenas falta el
  usuario, la empresa o el país. Degradar a una clave parcial parece inofensivo y es exactamente el
  mecanismo de la fuga: dos sesiones colapsan en la misma clave y la segunda lee lo de la primera.
  El `0` cuenta como ausencia — un chequeo con `!= null` lo dejaría pasar.

- **`decidir-cacheable.funcion.ts`** — lista **blanca** de endpoints y solo `GET`. Una lista negra
  deja entrar todo lo que nadie se acordó de excluir. Lo que queda fuera está fuera por escrito:
  dinero (costos, liquidaciones, contabilidad), identidad (auth, users, roles, permisos), reportes
  y herramientas internas.
  ⚠️ **Los nombres son cadenas sueltas y nada los ata a las URL reales.** Un typo no rompe el build ni
  ningún test: solo hace que esa pantalla no ande sin red, y eso se descubre en la granja. Por eso
  existe `frontend/scripts/verificar-lista-cacheable.js`, que **corre en CI y corta el gate de
  tests** si aparece un endpoint sin decisión o una entrada que la app nunca pide. Corrélo al tocar
  esta lista (`--informe` para mirar sin bloquear).

- **`vigencia-cache.funcion.ts`** — TTL **duro** de 16 h (la jornada offline de la decisión D4).
  Vencida no se sirve. La alternativa —mostrar siempre lo último con un cartel de "datos de hace 3
  días"— no alcanza: un cartel no compite con un número concreto en pantalla, y el operario decide
  mirando el número. Un `guardadoEn` en el futuro también vence: con el reloj corrido no se puede
  afirmar nada sobre la antigüedad.

## Reutilización

Nada acá depende del dominio avícola ni de un país: son reglas de caché particionada. Si se extrae un
paquete compartido, este directorio se copia tal cual.
