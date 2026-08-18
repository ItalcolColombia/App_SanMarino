# Correo para el administrador de Microsoft 365

> Copiá el texto de abajo y enviálo. Lo único que tenés que completar es el destinatario y tu firma.
> El detalle técnico completo está en [`CORREO_PROD_INFORME_TECNICO.md`](CORREO_PROD_INFORME_TECNICO.md) — conviene adjuntarlo.

---

**Asunto:** Solicitud: habilitar el envío SMTP del buzón zootecnico@sanmarino.com.co desde la aplicación ItalGranja

---

Hola,

Les escribo porque la plataforma ItalGranja (`zootecnico.sanmarino.com.co`) **no puede enviar correos
desde el 3 de junio de 2026** — a hoy, **75 días**. En ese período quedaron **71 correos sin
entregar**, y no son avisos prescindibles:

| Correo que no salió | Cantidad | Qué significa para la persona |
|---|---|---|
| Recuperación de contraseña | **20** | Se quedó afuera del sistema y no puede volver a entrar por sus propios medios |
| Bienvenida / alta de usuario | **16** | Nunca recibió sus credenciales de acceso |
| Notificaciones de tickets | **35** | No se entera de la creación, asignación ni solución de su caso |

El último intento fallido es del **15 de agosto de 2026**, así que el problema sigue activo.

**Ya verificamos que el problema no está en nuestra aplicación.** El 12 de agosto probamos, contra el
mismo buzón y con las mismas credenciales:

1. La autenticación SMTP **funciona** desde nuestra red corporativa: el servidor responde
   `235 Authentication successful`.
2. Un envío real **se entregó correctamente** ejecutando el mismo código del sistema.
3. El flujo completo de la aplicación funcionó de punta a punta.

Lo único que cambia entre el caso que funciona y el que falla es **desde dónde se conecta**. Cuando la
conexión sale de nuestro servidor de producción (AWS, región Ohio), Microsoft responde:

> **535 5.7.139** Authentication unsuccessful, the request did not meet the criteria to be
> authenticated successfully. **Contact your administrator.**

Por eso les escribimos.

## Qué necesitamos

Que el buzón **zootecnico@sanmarino.com.co** pueda autenticarse por SMTP desde nuestra aplicación de
servidor. Concretamente, les pedimos verificar tres cosas:

**1. Acceso condicional / Valores predeterminados de seguridad**
¿Hay alguna directiva que bloquee la *autenticación heredada* (legacy authentication) según la
ubicación o la IP de origen? Es lo que mejor explica el síntoma: el mismo usuario y contraseña
autentican desde la oficina y son rechazados desde el servidor. Necesitaríamos que esta aplicación
quede excluida de esa directiva.

**2. SMTP AUTH habilitado en los dos niveles.** Ambos comandos deberían devolver `False`:

```powershell
Get-CASMailbox 'zootecnico@sanmarino.com.co' | Select-Object SmtpClientAuthenticationDisabled
Get-TransportConfig | Select-Object SmtpClientAuthenticationDisabled
```

**3. Registros de inicio de sesión** (Entra ID → Inicios de sesión), filtrando por
`zootecnico@sanmarino.com.co` y protocolo *Authenticated SMTP*. Ahí debería verse exactamente qué
directiva produce el rechazo.

## Un detalle importante antes de autorizar por IP

Si la solución pasara por autorizar nuestra dirección de origen, hay que saber que **hoy esa IP
cambia en cada despliegue** (el servidor toma una IP pública del conjunto de AWS; el 12 de agosto era
`3.137.144.7`, pero no es estable). Autorizar esa dirección funcionaría hasta la próxima
actualización del sistema y volvería a romperse.

Tenemos dos formas de resolverlo, y necesitamos su opinión sobre cuál prefieren:

- **Opción A:** permitir la autenticación SMTP de este buzón **sin condicionarla a la ubicación**.
  Es la más simple y no depende de nosotros hacer cambios.
- **Opción B:** si la directiva debe seguir atada a ubicaciones, pedimos a nuestra área de AWS que nos
  asigne una **IP de salida fija**, se las informamos y ustedes autorizan esa única dirección. Toma
  más tiempo pero es igual de válido.

## Si la política no se puede levantar

Entendemos que puede haber una razón de seguridad para no habilitar la autenticación básica. En ese
caso, la alternativa correcta es **migrar a OAuth 2.0 con Microsoft Graph**, que no usa contraseña.
Para eso necesitaríamos que registren una aplicación en Entra ID con permiso `Mail.Send` acotado a
este buzón, y nosotros hacemos el desarrollo del lado del sistema. Avísennos si prefieren este camino
y coordinamos.

## Datos técnicos de la conexión

| Parámetro | Valor |
|---|---|
| Servidor | `smtp.office365.com` |
| Puerto | `587` (STARTTLS, TLS 1.2 — verificado) |
| Usuario / remitente | `zootecnico@sanmarino.com.co` |
| Origen que funciona | Red corporativa (IP `186.86.52.65`) |
| Origen que falla | AWS ECS, región us-east-2 (IP variable) |
| Último envío exitoso | 3 de junio de 2026 |
| Último intento fallido | 15 de agosto de 2026 |
| Correos sin entregar | 71 |

Quedo atento a lo que necesiten de nuestro lado. Adjunto el informe técnico con el detalle de todas
las pruebas.

Gracias,
