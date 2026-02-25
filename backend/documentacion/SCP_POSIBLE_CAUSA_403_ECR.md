# Posible causa del 403: política SCP (Service Control Policy)

El administrador indica que **puede ser una política SCP** aplicada en la organización/cuenta.

---

## ¿Qué es una SCP?

Las **Service Control Policies (SCP)** son políticas a nivel de **AWS Organizations**. Se aplican a cuentas (o a UOs) y **restringen qué acciones pueden hacer** los IAM users/roles de esa cuenta, aunque tengan permisos en sus políticas IAM.

- Si una SCP tiene **Deny** sobre una acción o recurso, **nadie** en la cuenta (o en la OU afectada) puede hacerla, aunque IAM lo permita.
- Eso explicaría por qué el **backend** a veces funcionaba y el **frontend** no: la SCP podría estar denegando solo ciertos recursos (por nombre, ARN o patrón) y el repo del frontend podría estar cayendo en ese Deny.

---

## Cómo puede estar afectando al push de ECR

Escenarios posibles:

1. **Deny sobre ECR** en ciertas condiciones (por ejemplo solo en algunos repos o por patrón de nombre).
2. **Deny sobre `ecr:PutImage`** o **`ecr:BatchGetImage`** que aplique al repositorio `sanmarino/zootecnia/granjas/frontend` y no al de backend.
3. **Deny por recurso:** la SCP permite ECR solo para ciertos ARN y el ARN del frontend no está en la lista.

El 403 en el **HEAD** (verificación del manifest) podría ser un **Deny** sobre la acción que ECR usa para esa verificación (por ejemplo lectura del manifest), y esa acción podría estar restringida por SCP para este repo o para este usuario.

---

## Qué debe revisar el administrador

### 1. Ver si la cuenta está en una organización y tiene SCPs

- **AWS Organizations** → seleccionar la cuenta **196080479890** (o la OU donde esté).
- Pestaña **Policies** / **Service control policies**.
- Ver qué SCPs están **attached** a la cuenta (o a la OU padre).

### 2. Revisar el contenido de cada SCP aplicada

- Abrir cada política SCP.
- Buscar:
  - **Effect: Deny**
  - Cualquier mención a **ECR**, **ecr:**, **elastic-container-registry**.
  - **Resource** o **Condition** que usen ARN de repositorios o patrones como `*frontend*`, `*granjas*`, etc.

### 3. Posibles ajustes si la SCP es la causa

- **Excluir el repositorio frontend** del Deny (por ejemplo con una condición que no aplique a `sanmarino/zootecnia/granjas/frontend`).
- **Añadir una excepción** en la SCP para las acciones ECR necesarias (p. ej. `ecr:PutImage`, `ecr:BatchGetImage`, etc.) para el ARN del repo frontend.
- Si la SCP restringe por patrón de recurso, **incluir** en el permiso el ARN:
  `arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/frontend`

### 4. ARN del recurso afectado (para copiar en la SCP o excepciones)

```
arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/frontend
```

---

## Resumen para el administrador

| Tema | Detalle |
|------|--------|
| Síntoma | 403 Forbidden al hacer push de la imagen del frontend a ECR (repo `sanmarino/zootecnia/granjas/frontend`). |
| Posible causa | SCP que aplica **Deny** sobre ECR (o sobre ciertos repos/acciones) y que estaría afectando a este repositorio o a la verificación del manifest. |
| Acción | Revisar SCPs aplicadas a la cuenta/OU, buscar Deny sobre ECR o sobre este ARN, y añadir excepción o ampliar el recurso para permitir las acciones ECR necesarias sobre el repo frontend. |

Si confirman que es una SCP, con que ajusten la política para que **no deniegue** las acciones ECR sobre `sanmarino/zootecnia/granjas/frontend` (o sobre el usuario/rol que hace el push), el push debería dejar de devolver 403.
