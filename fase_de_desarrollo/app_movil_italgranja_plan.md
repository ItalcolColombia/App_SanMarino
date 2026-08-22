# App móvil ItalGranja (Flutter) — Fase 1: login + SQLite + engorde/reproductora

> **Estado del arte al arrancar (21-ago-2026):** `zootecnicoapp/` ya tiene el design system
> completo traducido a Flutter (4.125 líneas: tema, widgets, 4 pantallas de seguimiento,
> `local_db.dart`, `sync_service.dart`). **Todo funciona con datos `_lotesDemo` hardcodeados en
> `main.dart`** y `SyncService` nunca llama al backend: su `_procesarCola()` simula el envío.
> Esta fase reemplaza esa simulación por el backend real, sin tocar el diseño.

## Alcance de esta fase

| Entra | No entra (fases siguientes) |
|---|---|
| Login real contra `/api/Auth/login` (AES) | Levante y Producción (ya existe la UI, falta el mapeo) |
| SQLite: sesión, lotes, catálogo, cola de sync | Ítems de inventario con descuento de stock |
| Módulos visibles = menú del usuario | Edición/borrado de registros ya sincronizados |
| Seguimiento diario **Pollo Engorde** | Fotos, firmas, adjuntos |
| Seguimiento diario **Reproductora Pollo Engorde** | Deploy / build release / firma de APK |
| Perfil **Ecuador y Panamá** resuelto por país | |

---

## 1. Contrato del backend — MEDIDO, no supuesto

Verificado el 21-ago-2026 contra `http://localhost:5002` con `admin.ecuador@italcol.com`
(script `scratchpad/smoke_login.js`, 200 en todos los pasos).

### 1.1 El login va cifrado en las dos direcciones

`POST /api/Auth/login` **no acepta JSON plano**. `AuthController.Login` recibe
`{ "encryptedData": "<base64>" }` y responde `text/plain` con otro base64:

```
request  : AES-256-CBC( JSON(LoginDto),   key = Encryption:RemitenteFrontend )
response : AES-256-CBC( JSON(AuthResponse), key = Encryption:RemitenteBackend )
```

El esquema exacto (`EncryptionService.cs`, espejado en `frontend/src/app/core/auth/encryption.service.ts`):

| Parámetro | Valor |
|---|---|
| Cifrado | AES-256-CBC, padding PKCS7 |
| Derivación de llave | PBKDF2-HMAC-**SHA256**, **10.000** iteraciones, 32 bytes |
| Salt | `sanmarino-salt` (fijo, literal) |
| IV | 16 bytes aleatorios, **prepend** al ciphertext |
| Encoding | Base64 del bloque `IV ‖ ciphertext` |

Las llaves salen de `appsettings.Development.json` (`Encryption:*`, `PlatformSecret:*`) — la app
las lleva en `--dart-define`, nunca hardcodeadas en el repo (§4.1).

### 1.2 Toda ruta que no sea el login exige `X-Secret-Up`

`PlatformSecretMiddleware` rechaza con **401** cualquier petición sin el header
`X-Secret-Up` = `AES(PlatformSecret:SecretUpFrontend, key = PlatformSecret:EncryptionKey)`.
Están exentos solo `/auth/login`, `/auth/register`, `/auth/recover-password`, `/ping`, `/health`.

**Ese 401 no es "tu sesión venció".** El middleware lo tipifica con la cabecera
`X-Auth-Failure: platform-secret`. El cliente móvil debe distinguirlo igual que el web
(`debe-cerrar-sesion-por-401.funcion.ts`): si viene esa cabecera, **no** borrar la sesión ni la
cola de sync — rotar el SECRET_UP en el servidor destruiría la cola de todos los dispositivos a
la vez.

### 1.3 Headers de toda petición autenticada

```
Authorization         : Bearer <jwt>
X-Secret-Up           : <secret up cifrado>
X-Device-Id           : <uuid estable del dispositivo>
X-Active-Company      : ItalcolEcuador
X-Active-Company-Id   : 3
X-Active-Pais         : 2
X-Active-Pais-Nombre  : Ecuador
```

`ActiveCompanyMiddleware` valida la empresa contra `user_companies`: si el usuario no la tiene
asignada, la ignora y el scope queda vacío. La empresa **nunca** se toma del header crudo: se
manda la que vino en `companyPaises` del login.

### 1.4 Endpoints de esta fase

| Qué | Método y ruta | Notas |
|---|---|---|
| Login | `POST /api/Auth/login` | cifrado ida y vuelta |
| Menú del usuario | `GET /api/Auth/menu?companyId=` | **respuesta cifrada** (`RemitenteBackend`) |
| Sesión | `GET /api/Auth/session?companyId=` | JSON plano |
| Lotes engorde | `GET /api/LoteAveEngorde` | 124 filas para Ecuador en local |
| Lotes reproductora | `GET /api/LoteReproductoraAveEngorde` | `[]` en Ecuador, 121 en Panamá |
| **Crear seguimiento engorde** | `POST /api/SeguimientoAvesEngordeEcuador` | ver ⚠️ abajo |
| Registros del lote (engorde) | `GET /api/SeguimientoAvesEngordeEcuador/por-lote/{loteId}` | para saber qué días ya tienen registro |
| **Crear seguimiento reproductora** | `POST /api/SeguimientoDiarioLoteReproductora` | |
| Registros del lote (reproductora) | `GET /api/SeguimientoDiarioLoteReproductora/por-lote-reproductora/{id}` | |

> ⚠️ **El controller se llama "Ecuador" pero atiende a los tres países.** No hay un camino por
> país: el front web postea a `SeguimientoAvesEngordeEcuador` para Ecuador, Panamá y Colombia, y
> los dos services escriben la misma tabla `seguimiento_diario_aves_engorde`. La tabla `_ecuador`
> no existe en la BD. La app móvil hace lo mismo que el web — el nombre miente por historia.

### 1.5 Un registro por lote por día

El backend responde **400** con `"Ya existe un registro de seguimiento diario para este lote en la
fecha seleccionada"` ante la violación de índice único (23505). Para una app offline esto es el
caso normal, no un error: dos dispositivos sin red pueden encolar el mismo día. Se trata como
**conflicto resuelto**, no como fallo a reintentar (§3.3).

---

## 2. Los formularios: qué campos y de dónde salen

### 2.1 Engorde → `CreateSeguimientoLoteLevanteRequest`

`loteId` = `loteAveEngordeId`. Campos que la app manda:

| Sección | Campos |
|---|---|
| General | `fechaRegistro`, `observaciones`, `ciclo` (`"Normal"`) |
| Mortalidad y selección | `mortalidadHembras`, `mortalidadMachos`, `selH`, `selM`, `errorSexajeHembras`, `errorSexajeMachos` |
| Alimento | `tipoAlimento`, `consumoKgHembras`, `consumoKgMachos` |
| Peso | `pesoPromH`, `pesoPromM`, `uniformidadH`, `uniformidadM`, `cvH`, `cvM` |
| Agua *(EC + PA)* | `consumoAguaDiario`, `consumoAguaPh`, `consumoAguaOrp`, `consumoAguaTemperatura` |
| Quintales *(solo PA)* | `qqMixtas`, `qqHembras`, `qqMachos` |

### 2.2 Reproductora → `CreateSeguimientoDiarioLoteReproductoraRequest`

`loteId` = `lote_reproductora_ave_engorde.id`. Mismos campos menos los de huevos de levante;
`consumoHembras`/`consumoMachos` con `unidadConsumoHembras`/`unidadConsumoMachos` (`"kg"`/`"g"`).

### 2.3 La diferencia por país es un dato, no un `if` de empresa

Regla del repo: **prohibido** `if (empresa == 'ItalcolPanama')`. La decisión vive en una función
pura sobre el `paisId` de la sesión, que llega en `companyPaises[].paisId` del login:

```dart
// core/perfil_pais.dart — lógica pura, testeable, sin red ni estado
class PerfilPais {
  static bool controlAgua(int? paisId)  => paisId == kEcuador || paisId == kPanama;
  static bool quintales(int? paisId)    => paisId == kPanama;
}
```

`Usuario.tieneControlAgua` (hoy compara strings `'ecuador'`/`'panama'`) pasa a delegar aquí.

---

## 3. La base de datos local

### 3.1 Tablas (SQLite v2 — hoy va en v1)

| Tabla | Para qué | Estado |
|---|---|---|
| `pending_sync` | cola de envío | **existe** — se le agregan `endpoint`, `remote_id`, `resuelto_por` |
| `lotes_cache` | lotes para trabajar sin red | **existe** — se le agregan `company_id`, `pais_id`, `lote_reproductora_id`, `cerrado` |
| `items_cache` | catálogo de alimentos | **existe** |
| `sesion` | token, empresa, país, menú, `ultima_sync` | **nueva** |
| `registros_locales` | qué días ya tienen registro (propio o del server) | **nueva** |

La migración de v1→v2 va con `ALTER TABLE ... ADD COLUMN` en `onUpgrade` — un dispositivo ya
instalado no puede perder su cola pendiente.

### 3.2 Flujo de arranque

```
                     ┌── hay red ──► login online ──► descarga lotes+menú+catálogo ──► marca ultima_sync
usuario abre la app ─┤
                     └── sin red ──► ¿hay sesión guardada? ──sí──► entra en modo offline
                                                            └─no──► "necesitás conexión la primera vez"
```

La regla que pediste — **una sincronización diaria obligatoria** — se implementa como aviso, no
como bloqueo: si `ultima_sync` no es de hoy, la home muestra el chip ámbar *"Sincronizá hoy"*.
Bloquear al usuario en un galpón sin señal sería peor que dejarlo registrar.

### 3.3 Cola de sincronización — reemplaza la simulación actual

`SyncService._procesarCola()` hoy hace `Future.delayed` y marca todo como enviado. Pasa a:

1. Tomar los `pending` en orden de `created_at`.
2. `POST` al endpoint que dice la fila (`endpoint` + `payload`).
3. **201/200** → `estado='sincronizado'`, guarda `remote_id`.
4. **400 duplicado** (23505) → `estado='duplicado'`: el día ya existe en el servidor. No es un
   error del usuario; se le muestra como *"ya estaba registrado"* y sale de la cola.
5. **401 con `X-Auth-Failure: platform-secret`** → parar la cola, **no** cerrar sesión.
6. **401 sin esa cabecera** → token vencido: parar la cola y pedir re-login. La cola sobrevive.
7. **red caída / 5xx** → `intentos++`, backoff, queda `pending`.

---

## 4. Archivos

### 4.1 Nuevos

```
zootecnicoapp/lib/core/
├── config/api_config.dart          # baseUrl + llaves por --dart-define (defaults de dev local)
├── crypto/crypto_service.dart      # AES-256-CBC + PBKDF2-SHA256 — espejo de EncryptionService.cs
├── api/api_client.dart             # Dio + interceptor de headers + tipificación del 401
├── api/auth_api.dart               # login (cifrado), menu (cifrado), session
├── api/lotes_api.dart              # LoteAveEngorde + LoteReproductoraAveEngorde → Lote
├── api/seguimientos_api.dart       # POST engorde / reproductora + días ya registrados
├── session/session_store.dart      # persiste sesión en SQLite; expone empresa/país activos
├── perfil_pais.dart                # decisiones por país (agua, quintales) — lógica pura
└── modulos_del_menu.dart           # menú del usuario → List<ModuloSeguimiento> — lógica pura
zootecnicoapp/test/
├── crypto_service_test.dart        # vectores del backend: descifra lo que cifró .NET
├── perfil_pais_test.dart
└── modulos_del_menu_test.dart
```

### 4.2 Modificados

| Archivo | Cambio |
|---|---|
| `pubspec.yaml` | + `pointycastle`, `uuid`, `crypto` |
| `lib/main.dart` | fuera `_lotesDemo`; arranque real (§3.2) |
| `lib/core/models.dart` | `Usuario` con `paisId`/`companyId`/`token`; `Lote` con ids reales |
| `lib/core/local_db.dart` | v2 + tablas `sesion` y `registros_locales` |
| `lib/core/sync_service.dart` | cola real (§3.3) |
| `lib/screens/login_screen.dart` | llama a `AuthApi.login` |
| `lib/screens/seguimiento_screen.dart` | arma el payload por módulo y país |

---

## 5. Reglas de negocio

1. **Empresa y país por dato, fail-closed.** Salen de `companyPaises[0]` del login. Si el login no
   trae empresa, la app no deja registrar: mostrar *"tu usuario no tiene empresa asignada"*.
2. **Módulos = menú del usuario.** `admin.ecuador` tiene *Pollo Engorde* pero **no**
   *Reproductora Pollo Engorde*; `admin.panama` tiene los dos. Se mapea por `route`, nunca por id
   (los ids difieren local↔prod). Sin la ruta en el menú → el módulo no aparece.
3. **El registro local es la verdad hasta que el servidor lo confirme.** Guardar nunca falla por
   red.
4. **Offline nunca es rojo** (regla de UX del design system): es un modo de trabajo válido.
5. **Un lote cerrado (`estadoOperativoLote = 'Cerrado'`) no admite registros** — se cachea pero se
   muestra deshabilitado.

## 6. Casos de prueba

### Unitarios (`flutter test`)
- `crypto_service_test` — descifrar un base64 producido por `EncryptionService.cs` y verificar que
  lo cifrado por Flutter lo descifra el backend (round-trip contra vectores capturados).
- `perfil_pais_test` — Ecuador: agua sí, quintales no · Panamá: los dos · Colombia: ninguno.
- `modulos_del_menu_test` — menú de `admin.ecuador` → `[engorde]`; el de `admin.panama` →
  `[engorde, reproductora]`; menú vacío → `[]`.

### Smoke contra el back local (`tool/smoke_backend.dart`)

Corre con el **mismo código de la app** — es la única prueba de que el cifrado de
Dart y el de .NET son compatibles de verdad. Ejecutado el 21-ago-2026, **8/8 en
los dos perfiles**:

```bash
dart run tool/smoke_backend.dart admin.ecuador@italcol.com 123456789
dart run tool/smoke_backend.dart admin.panama@italcol.com  123456789
```

| # | Paso | Ecuador (pais 2) | Panamá (pais 3) |
|---|---|---|---|
| 1 | Login cifrado | ✅ `ItalcolEcuador` (3) | ✅ `ItalcolPanama` (5) |
| 2 | Clave incorrecta → 401 | ✅ | ✅ |
| 3 | `X-Secret-Up` inválido → 401 con `X-Auth-Failure`, sesión intacta | ✅ | ✅ |
| 4 | Menú descifrado → módulos | ✅ Pollo Engorde | ✅ Engorde + Reproductora |
| 5 | Lotes descargados | ✅ 124 (30 abiertos) | ✅ 144 (83 abiertos) |
| 6 | POST seguimiento → 201 | ✅ | ✅ |
| 7 | Segundo registro del mismo día → rechazado | ✅ duplicado (23505) | ✅ doble validación |
| 8 | DELETE del registro de prueba | ✅ | ✅ |

Verificado en la BD: **0 filas** con `observaciones LIKE 'SMOKE%'` en las dos
tablas de seguimiento.

> El paso 7 se rechaza de dos formas legítimas y las dos valen: sin el flag
> `requiere_validacion_seguimiento_diario` llega al índice único; **con** el flag
> (Panamá) una guarda previa corta porque el registro del paso 6 quedó sin
> validar. Lo que se comprueba es que la app **deja de reintentar**, no cuál de
> las dos guardas actuó.

---

## 7. Hallazgos en el backend (✅ CORREGIDOS el 21-ago-2026)

> Los dos se cerraron en su propio plan:
> [`alimento_obligatorio_consumo_escalar_reproductora_produccion_plan.md`](alimento_obligatorio_consumo_escalar_reproductora_produccion_plan.md).
> `dotnet build` 0/0 y `dotnet test` 3.049/3.049. El texto de abajo se conserva como el
> diagnóstico original.

Los dos salieron del smoke, con el flag `requiere_validacion_seguimiento_diario`
encendido (hoy **sólo `ItalcolPanama`**, id 5).

### 7.1 Reproductora y Producción ignoran el consumo escalar al exigir alimento

`SeparacionSeguimientoHelper.ValidarAlimentoObligatorio` recibe
`kgHembrasDirecto` / `kgMachosDirecto` **precisamente** para el cliente que manda
el consumo como campo suelto en vez de como ítems de inventario — su propio
doc-comment lo dice: *«No es un caso raro: es como cargan los clientes que no
pasan por el formulario. Sin mirarlo, el guard rechazaba un registro que SÍ
traía alimento»*.

Levante y los dos engordes se los pasan. **Reproductora y Producción no:**

| Módulo | Llamada | Pasa los directos |
|---|---|---|
| Levante | `SeguimientoLoteLevanteService.Crud.cs:38` | ✅ |
| Engorde | `SeguimientoAvesEngordeService.Crud.cs:97` · `…Ecuador…Crud.cs:41` | ✅ |
| **Reproductora** | `SeguimientoDiarioLoteReproductoraService.cs:267` y `:384` | ❌ |
| **Producción** | `ProduccionService.Seguimiento.cs:238` y `:628` | ❌ |

`MetadataEngordeCalculos.ParseKgPorBloque` sólo suma ítems con
`catalogItemId`/`itemInventarioEcuadorId` > 0, así que un registro con
`consumoHembras: 120` y sin ítems cuenta como **cero kilos**.

**Efecto medido:** en Panamá, `POST /api/SeguimientoDiarioLoteReproductora` con
alimento cargado como escalar responde 400 *«no tiene alimento»*. Con el flag
apagado en local, el mismo request se creó bien (id 791) — o sea que el bloqueo
es esa rama, no el payload. Afecta a la app móvil, a la carga masiva por Excel y
a cualquier integración que no pase por el formulario web.

**Arreglo (hecho):** las cuatro llamadas pasan los directos. En Producción se pasan las
variables ya normalizadas a kg (`consumoKgH`/`consumoKgM`) y no `request.ConsumoH`,
que viene **con unidad** y puede estar en gramos. La combinación MAX metadata-vs-escalar
bajó a `AlimentoObligatorioCalculos.Capturado(...)` para poder cubrirla con tests
(`Application.Tests` no referencia Infrastructure). 17 tests xUnit nuevos.

**De paso:** el patrón que se copiaba tenía su propio bug. `(decimal)dto.ConsumoKgMachos!`
sobre un `double?` **desenvuelve y lanza** `InvalidOperationException("Nullable object must
have a value.")`, que el controller traduce a un 400 ilegible — y ese campo llega `null`
siempre que el registro no tiene alimento de machos. Las seis llamadas (las cuatro nuevas
más las tres viejas, alta y edición) usan ahora `(decimal)(x ?? 0)`.

### 7.2 El duplicado de reproductora sale como 500, no como 400

`SeguimientoDiarioLoteReproductoraController.Create` no tiene el
`catch (DbUpdateException … 23505)` que sí tiene el de engorde: la violación del
índice único cae en el `catch (Exception)` genérico y vuelve como **500** con el
mensaje crudo de Postgres.

**Estaba mitigado en la app:** `ApiClient` detecta el duplicado por **contenido**
(`23505` / `duplicate key value` / el texto redactado), no por el status. Sin eso, la cola
reintentaría para siempre un día que ya está guardado.

**Arreglo (hecho):** el `catch` está copiado en `Create`, con el mismo texto que el de
Ecuador y **antes** del `catch (Exception)` genérico — C# evalúa en orden y el genérico se
lo comía. La detección por contenido de la app sigue siendo válida y no se tocó.
