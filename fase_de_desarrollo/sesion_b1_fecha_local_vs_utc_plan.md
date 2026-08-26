# La revocación de sesión juzga las fechas 5 horas antes de tiempo

> Encontrado el 26-ago-2026 mientras se hacía el smoke de [`menu_efectivo_por_empresa_plan.md`](menu_efectivo_por_empresa_plan.md):
> una sesión que vencía en **1 hora** era rechazada con `token-expirado`, con la fila viva en la base.

---

## 1. El defecto, medido

`Program.cs:128` activa `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)`. Con ese
switch, una columna **`timestamptz` vuelve de la BD como `DateTime` con `Kind = Local`**, o sea
convertida a la hora de la máquina (−05).

`RevocacionSesionCalculos.Evaluar` compara `expiresAt <= ahoraUtc`, con `ahoraUtc = DateTime.UtcNow`.
**La comparación de `DateTime` en .NET es numérica e ignora el `Kind`**: compara una hora local contra
una hora UTC. En una máquina en −05 la sesión se juzga vencida **5 horas antes**.

Medido contra el backend real (`:5499`), fila puesta a mano en `sesiones_activas`:

| `expires_at` de la fila | En SQL | Respuesta del backend |
|---|---|---|
| `now() + 1 hora` | viva (`expires_at > now()`) | **401 `token-expirado`** |
| `now() + 7 horas` | viva | **200 OK** |

El salto entre las dos es exactamente el offset de la máquina.

### El `Kind` está confirmado, no supuesto

`GET /api/Session/mias` devuelve las fechas tal como salen de la entidad:

```json
"createdAt":"2026-08-26T07:24:49.678621-05:00"
```

System.Text.Json **sólo emite offset para `DateTimeKind.Local`**. Confirma que Npgsql legacy devuelve
`Kind = Local`, y por lo tanto que `ToUniversalTime()` es la conversión correcta (no un
`SpecifyKind`).

### Lo que NO está roto: las comparaciones que corren en SQL

`ListarDeUsuarioAsync`, `RevocarTodasDelUsuarioAsync` y `LimpiarVencidasAsync` filtran con
`s.ExpiresAt > ahora` **dentro de la consulta**, así que compara Postgres y el parámetro viaja con el
instante correcto. Verificado: una fila vencida hace 2 h **no** aparece en `/api/Session/mias`.

**La asimetría es el punto:** en este repo, con el switch legacy puesto, una fecha de BD comparada
**en SQL** está bien y la misma fecha comparada **en memoria** está corrida. Sólo hay que arreglar las
segundas.

### Los dos puntos afectados

| Dónde | Qué compara | Efecto |
|---|---|---|
| `SesionActivaService.EvaluarAsync` → `Evaluar(expiresAt: fila.ExpiresAt, …)` | vencimiento de la sesión | **Recorta 5 h a cada sesión.** Es el defecto reportado. |
| `SesionActivaService.TocarAsync` → `DebeActualizarUltimaVista(fila.LastSeenAt, …)` | antigüedad de `last_seen_at` | `ahora − ultimaVista` sale inflado 5 h ⇒ **siempre** supera el umbral y el throttle de escritura no frena nada. Escrituras de más, no un error visible. |

`InvalidarCache` ya normaliza con `ANormalizarUtc` (que convierte `Local → Utc`), así que estaba bien.

---

## 2. Qué se cambia, y dónde

**La normalización se mete en la parte PURA**, no en el service. Así el arreglo queda cubierto por
tests y ningún call site futuro puede volver a reintroducirlo pasando una fecha cruda de la base.

- `RevocacionSesionCalculos.AUtc(DateTime)` + sobrecarga nullable — Utc queda igual, Local se
  convierte, `Unspecified` se asume UTC (semántica idéntica a la que ya tenía
  `SesionActivaService.ANormalizarUtc`, que no cambia).
- `Evaluar`, `DebeActualizarUltimaVista` y `TtlCache` normalizan sus argumentos de fecha al entrar.
- `SesionActivaService.ANormalizarUtc` pasa a **delegar** en la pura: una sola definición de la
  conversión en todo el módulo.
- **`EvaluarAsync` y `TocarAsync` no se tocan**: con la regla adentro de la parte pura, sus llamadas
  ya quedan correctas.

**Lo que NO se cambia, a propósito:**
- **`ToDto`** sigue entregando las fechas tal cual. Con `Kind = Local` el JSON sale con offset
  (`-05:00`), que es el instante correcto y el front lo renderiza bien. Normalizarlas a UTC cambiaría
  el formato del cable sin arreglar nada visible.
- **El switch legacy** (`Program.cs:128`) se deja puesto. Sacarlo cambia el mapeo de fechas de **todo**
  el proyecto —112 tablas, funciones SQL que asumen `America/Bogota`— y no es este arreglo.
- **Las consultas que filtran en SQL**, que ya están bien.

---

## 3. Archivos

- `backend/src/ZooSanMarino.Application/Calculos/RevocacionSesionCalculos.cs` — `AUtc` + normalización
  al entrar en las tres funciones.
- `backend/src/ZooSanMarino.Infrastructure/Services/SesionActivaService.cs` — el helper privado
  delega en la pura.
- `backend/tests/ZooSanMarino.Application.Tests/RevocacionSesionCalculosTests.cs` — casos nuevos.

Sin migración: no toca la BD. Sin cambios en el front.

---

## 4. Casos de prueba

Sobre `RevocacionSesionCalculosTests` (gate de CI):

1. **El caso medido** — `expiresAt` con `Kind = Local` una hora en el futuro ⇒ `Valida`, no `Vencida`.
2. `expiresAt` con `Kind = Local` ya pasado ⇒ `Vencida` (el arreglo no vuelve inmortal a nadie).
3. `Kind = Utc` ⇒ **byte a byte igual que antes** (Valida y Vencida), para probar que no se movió el
   comportamiento del caso que ya funcionaba.
4. `Kind = Unspecified` ⇒ se sigue tratando como UTC (semántica previa conservada).
5. Precedencia intacta: revocada gana sobre vencida, aun con `Kind = Local`.
6. `DebeActualizarUltimaVista` con `ultimaVista` en `Local` recién escrita ⇒ **false** (hoy da true por
   las 5 h fantasma).
7. `DebeActualizarUltimaVista` con `ultimaVista` en `Local` vieja de verdad ⇒ true.
8. `TtlCache` con `expiracionToken` en `Local` ⇒ el TTL no sale negativo ni recortado 5 h.
9. `AUtc(null)` ⇒ null.

**Smoke HTTP** (el mismo que destapó el defecto): fila con `expires_at = now() + 1 hora` ⇒ antes
**401 `token-expirado`**, después **200**. ⚠️ El veredicto muerto **se cachea hasta el `exp` del
token**, así que entre intento e intento hay que reiniciar el proceso o usar otro `jti`.

---

## 5. Validación

```bash
dotnet test    # RevocacionSesionCalculosTests + la suite entera
dotnet build   # 0 errores, sin advertencias nuevas
```
Más el smoke HTTP de arriba, con la fila borrada y el puerto libre al terminar.
