# `core/pwa/funciones/` — reglas puras del ciclo de vida de la PWA

Convención de CLAUDE.md ("CLEAN CODE — organización de funciones"): una función **pura** por
responsabilidad, sin `this`, sin DI, sin `HttpClient`, sin tocar `window` ni `navigator`. Los servicios
de `core/pwa/` son orquestadores delgados: leen el estado del navegador, se lo pasan a estas funciones,
y actúan sobre el resultado.

## Por qué estas tres reglas están acá y no adentro del servicio

Son las decisiones que, mal tomadas, rompen la app en campo — y son justamente las que no se pueden
probar desde un componente:

- **`decidirActualizacion`** — el servicio anterior (`VersionCheckService`, eliminado en esta misma
  entrega) llamaba `window.location.reload()` un segundo después de detectar una versión nueva, sin
  preguntar. Un galponero a mitad de un formulario perdía la captura. Peor: comparar mal las versiones
  produce un **bucle de recarga** que deja el dispositivo inutilizable. La regla es un `switch` de diez
  líneas y merece tests propios porque el costo de equivocarse es ese.

- **`formatearBytes`** — se usa en la pantalla de diagnóstico, que es la que el operario le manda por
  WhatsApp a soporte. Un número mal formateado ahí es un diagnóstico equivocado a distancia.

- **`resumirEstadoSw`** — traduce la combinación `soportado / registrado / controlando` a una etiqueta y
  un semáforo. El caso que importa es **"registrado pero no controla"**: en el primer load es normal, y
  a partir del segundo es el síntoma de que el SW se desactivó solo (safe mode).

## Reutilización

Nada acá depende de este dominio ni de este país: son reglas del ciclo de vida de una PWA. Si el
proyecto se replica para otra empresa o se extrae un paquete compartido, este directorio se copia tal
cual.
