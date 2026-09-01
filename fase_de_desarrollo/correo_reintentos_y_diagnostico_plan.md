# Cola de correo: reintentos que no agraven, y un diagnóstico que no mienta (1-sep-2026)

Sale de revisar por qué «no funcionaba el correo». El envío **ya estaba activo** en producción
(`Email:Queue:Enabled = true`) y funciona —dos envíos `sent` el 29-ago—, pero el manejo de errores
tiene dos defectos que hicieron perder días de diagnóstico y que pueden **agravar** la falla.

## 1. El diagnóstico miente, y se puede probar

`SmtpEmailSender.BuildSmtpExceptionDetails` evalúa las ramas en este orden:

1. `MustIssueStartTlsFirst` → «Office 365 requiere STARTTLS. **Solución: verificar que EnableSsl=true**»
2. `5.7.30` → auth básica retirada
3. `535 / 5.7.139 / 5.7.57` → el diagnóstico bueno, con qué pedirle al admin de M365

🔴 **La primera rama se come todos los fallos de autenticación.** .NET mapea a
`MustIssueStartTlsFirst` el `530` que Office 365 devuelve en el `MAIL FROM` *posterior* a un AUTH
fallido, así que la rama 3 —la única correcta— **es inalcanzable**. Es el mismo tipo de defecto que
`LOHMANN BROWN` cayendo en el token `LOHMANN`: **el orden de evaluación es parte del contrato**.

Medido en la cola (`email_queue` id 164, 28-ago):

```
Status Code: MustIssueStartTlsFirst
Message: ... 535 5.7.139 Authentication unsuccessful, account locked. Contact your administrator.
Diagnosis: Office 365 requiere STARTTLS antes de autenticarse.
Solución: Verificar que EnableSsl=true en configuración para puerto 587.
Configuración actual: EnableSsl=True, Port=587
```

El propio texto se contradice: dice «verificá que EnableSsl sea true» y a la línea siguiente informa
que **ya es true**. Manda a arreglar lo que está bien y esconde lo que importa: **la cuenta está
bloqueada**. Ese diagnóstico ya llevó una vez a construir una migración entera a Graph sobre una
premisa falsa.

Falta además la rama de `account locked`, que es un caso distinto de «el tenant rechaza por origen»
y tiene otra salida.

## 2. Los reintentos pueden sostener el bloqueo

3 intentos por correo, **sin espera entre uno y otro** más allá del ciclo de polling, y **sin
distinguir** si el error tiene sentido reintentarlo:

- Cuenta bloqueada o credenciales rechazadas ⇒ reintentar **no puede** funcionar, y cada intento es
  una autenticación fallida más contra el tenant: es lo que dispara y sostiene el lockout de M365.
  Entre el 26 y el 28-ago fueron 10 correos × 3 intentos = **30 autenticaciones fallidas**.
- Un timeout de red o un `4xx` transitorio ⇒ ahí sí conviene reintentar, con espera creciente.

Hoy los dos casos se tratan igual.

## 3. Qué se cambia

### A · `EmailErrorCalculos` — cálculo puro, con tests

Una sola función que clasifica el error a partir del **mensaje real del servidor**, no del
`StatusCode` que mapeó .NET:

| Clase | Cuándo | ¿Reintenta? |
|---|---|---|
| `CuentaBloqueada` | `account locked` | **No** |
| `AutenticacionRechazada` | `535`, `5.7.139`, `5.7.57`, `Authentication unsuccessful` | **No** |
| `AuthBasicaRetirada` | `5.7.30`, `Basic authentication is not supported` | **No** |
| `BuzonInvalido` | `550`, `5.1.1`, `RecipientNotFound` | **No** |
| `RequiereStartTls` | `MustIssueStartTlsFirst` **y ningún código de auth en el mensaje** | Sí |
| `Transitorio` | timeout, `4.x.x`, conexión caída | Sí |
| `Desconocido` | lo demás | Sí |

Cada clase trae su diagnóstico y su acción concreta. El orden de evaluación queda **fijado por
tests**, con los mensajes textuales medidos en producción, para que nadie lo reordene sin romper algo.

### B · Reintentos con backoff, y que respeten lo permanente

- Un error permanente marca `failed` **en el primer intento**: no gasta los 3 ni sigue golpeando al
  tenant.
- Los transitorios esperan con backoff exponencial (1 min, 5 min, 15 min) en vez de reintentar en el
  siguiente ciclo. Columna nueva `next_retry_at`, y el procesador solo toma lo que ya venció.

### C · Lo que se guarda

`error_type` pasa a ser la clase (`cuenta_bloqueada`, `autenticacion_rechazada`, …) en vez de
`max_retries_exceeded`, que no dice nada de la causa. El mensaje conserva el detalle SMTP completo
—host, puerto, remitente, destinatario— porque eso sí sirvió, pero con el diagnóstico correcto.

## 4. Casos de prueba

1. El mensaje textual del id 164 clasifica como `CuentaBloqueada`, **no** como `RequiereStartTls`.
2. El mensaje de agosto (`did not meet the criteria…`) clasifica como `AutenticacionRechazada`.
3. Un `MustIssueStartTlsFirst` **sin** código de auth sigue clasificando como `RequiereStartTls`.
4. Los permanentes no reintentan; los transitorios sí, con la espera creciente esperada.
5. `dotnet build` + `dotnet test` verdes; migración idempotente probada con `Up()`×2 y `Down()`.

⚠️ **Nada de esto arregla el correo**: si la cuenta vuelve a bloquearse, sigue siendo un tema del
admin de M365. Lo que cambia es que el sistema lo **diga bien** y deje de empeorarlo.
