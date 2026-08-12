# Auditoría — qué queda disponible sin conexión (12-ago-2026)

Responde tres preguntas: (1) el menú «Lote Reproductora» que no se usa, (2) si el usuario entra y ve
su menú sin internet, (3) qué acciones operativas funcionan sin red.

Todo medido contra la BD local (dump de prod) y el código de hoy.

---

## 1. El menú «Lote Reproductora» (id 9) — revisión aparte

| | |
|---|---|
| Ruta | `/daily-log/seguimiento-diario-lote-reproductora` |
| Etiqueta | **Lote Reproductora** |
| `company_menus` | **ninguna empresa** |
| `role_menus` | **3 roles**: Auxiliar de Granja, Líder técnico, Director técnico |
| Qué carga en el front | 🔴 **`SeguimientoLoteLevanteModule`** — el módulo de **levante**, no el de reproductora |

### Los dos hallazgos

**a) Está visible aunque no esté habilitado para ninguna empresa.** El sidebar se arma con
`role_menus`; `company_menus` configura pero no oculta. Esos tres roles ven hoy una entrada llamada
«Lote Reproductora».

**b) La entrada no abre lo que su nombre dice.** Carga el módulo de levante. O sea que lo que se
capture ahí es un seguimiento de **levante**, no de reproductora de postura.

El módulo de reproductora de postura **no existe todavía** como pantalla de captura: no hay endpoint
propio. El único `SeguimientoDiarioLoteReproductora` que existe es el de **pollo engorde**
(menú 43, exclusivo de ItalcolPanama).

### Qué significa para la PWA

**Nada que apagar.** No hay captura de reproductora de postura que pudiera encolarse: la lista blanca
del outbox no la incluye porque no existe. La entrada del menú, mientras cargue levante, se comporta
como levante y encola como levante — que es lo correcto para lo que realmente hace.

**Recomendación (no ejecutada, requiere decisión):** o se le quita el menú a esos 3 roles hasta que el
módulo exista, o se corrige la etiqueta. Dejarlo así hace que un técnico entre por «Lote
Reproductora» y cargue levante sin darse cuenta. Cuando el módulo se construya, **ahí sí** hay que
decidir explícitamente si entra al outbox.

---

## 2. Entrar sin internet y ver el menú

### ✅ Lo que SÍ funciona

- **El menú sobrevive sin red.** No se re-pide: vive en la sesión persistida (`auth_session` en
  localStorage, cifrada). `MenuService.ensureLoaded()` lee del subject, si no de **storage**, y solo
  va a la API si no hay ninguno. `preloadMyMenu()` ante un error hace
  `catchError(() => of(this.subject.value))` — cae al menú que ya tenía.
- Que `roles` esté en los EXCLUIDOS de la caché HTTP **no rompe nada**: el menú no se sirve de esa
  caché, se sirve de la sesión.
- **Perder la red no cierra la sesión** (F0.B/B2), con tope duro de 16 h sin contacto (D4).

### 🔴 Los dos límites reales

**a) El primer ingreso EXIGE red.** `POST /auth/login` es HTTP, y en prod además hay reCAPTCHA. Un
dispositivo donde ese usuario **nunca** entró no puede abrirse sin internet. De ahí el alistamiento en
oficina: **instalar + entrar una vez con señal, por cada usuario que vaya a usar esa tablet**.

**b) 🔴 El dispositivo guarda UNA sola sesión.** `TokenStorageService` usa una única clave
(`auth_session`). No hay «los usuarios registrados» en plural: entra el último que hizo login. Si en
la granja se turnan dos operarios en la misma tablet, **el segundo no puede entrar sin red** — y al
entrar con red, pisa la sesión del primero.

> ⚠️ Esto último choca con el requisito «tener el usuario o los usuarios que registró». Hoy es **uno**.
> Soportar varios exige sesiones multi-slot (una clave por usuario + selector de perfil), y hay que
> decidir qué pasa con la caché de consultas y el outbox de cada uno — la partición ya está lista para
> eso (`{userId}|{companyId}|{paisId}`), pero el storage de sesión no.

---

## 3. Acciones operativas sin red

Hay que separar **ver** de **guardar**. Son dos listas blancas distintas.

| Módulo | Ver sin red | Guardar sin red |
|---|---|---|
| Gastos de inventario (`inventario-gastos`) | ✅ | ❌ |
| Gestión de inventario (`inventario-gestion`, `inventario`) | ✅ | ❌ |
| Historial de inventario | ✅ | ❌ |
| Inventario de aves (`InventarioAves`) | ✅ | ❌ |
| Movimiento de aves (`MovimientoAves`) | ✅ | ❌ |
| Movimiento pollo engorde (+ Panamá) | ✅ | ❌ |
| Traslados (`traslados`, `TrasladoNavigation`) | ✅ | ❌ |
| Huevos — clasificación y arrastre | ✅ (dentro de producción/levante) | ❌ como movimiento propio |
| Venta de aves | ✅ (vía movimientos) | ❌ |
| **Captura diaria** levante · producción · pollo engorde · reproductora engorde | ✅ | ✅ **F3** |

**Sin red, hoy: se consulta todo eso, no se guarda nada de eso.** Un intento de guardar falla igual
que antes de la PWA (el interceptor solo encola lo que está en la lista blanca del outbox).

### Por qué no están, y qué costaría

No es un olvido: es la decisión **D1** del plan madre — *«escritura offline v1 = lista blanca de
captura diaria; ventas y movimientos a v2»*. La captura diaria es una hoja: escribe su fila y listo.
Los movimientos **no**:

- **Tocan stock y saldos** que otro dispositivo puede estar moviendo al mismo tiempo. Es la clase (b)
  del plan (*divergencia con el mundo*): «no hay stock suficiente» al sincronizar no es un error de
  captura, es un hecho físico ya ocurrido. Esa clase está **modelada pero sin emisor** todavía.
- **Traslados y ventas son operaciones de dos lados** (origen y destino, o lote y cliente). Encolar
  solo un lado deja el otro sin su contraparte.
- Varios crean **entidades que otras referencian** (un traslado con su grupo), y eso exige el grafo de
  operaciones con `client_entity_id` — explícitamente fuera de alcance en F3.

**Prerrequisitos abiertos antes de meter cualquiera de estos al outbox:** A4, B1 (revocación de
sesión), B8 (rotar llaves), B10, y emitir de verdad la clase `requiere_cuadre`.

---

## Resumen ejecutable

1. **Decidir** qué hacer con el menú id 9 (quitarlo de los 3 roles o renombrarlo). No es PWA.
2. **Alistamiento**: cada usuario debe entrar una vez con señal en la tablet que va a usar. Hoy,
   **un usuario por dispositivo**.
3. **Multi-sesión por dispositivo**: pendiente de decisión; es lo único que bloquea «varios usuarios
   sin internet».
4. **Movimientos offline**: es F4, con sus prerrequisitos. Hoy se consultan, no se guardan.
