# Validación: ¿Es por permisos IAM? ¿Qué permisos afectan la subida?

**Fecha:** 24-Feb-2026  
**Usuario:** moisesmurillo@sanmarino.com.co  
**Repositorio:** sanmarino/zootecnia/granjas/frontend

---

## 1. Resultado del deploy

El push sigue fallando con **403 Forbidden** en el HEAD request al manifest (igual que antes de los ajustes).

---

## 2. Validación por AWS CLI (mismo usuario)

Cada acción ECR sobre el repositorio frontend se probó con AWS CLI:

| Acción ECR | Resultado | ¿Afecta la subida? |
|------------|-----------|---------------------|
| `ecr:GetAuthorizationToken` | ✅ OK | No; el login funciona |
| `ecr:DescribeRepositories` | ✅ OK | No |
| `ecr:BatchCheckLayerAvailability` | ✅ OK | No; las capas se suben |
| `ecr:BatchGetImage` | ✅ OK | No; lectura de manifest permitida |
| `ecr:InitiateLayerUpload` | ✅ OK | No; se inicia la subida |
| `ecr:PutImage` | ❓ No simulable vía CLI | Es la que escribe el manifest |

**Conclusión:** Con el usuario actual, **todas las acciones ECR probadas están permitidas** sobre el repo frontend cuando se usan vía AWS CLI.

---

## 2.1 Validación: ¿Buildx usa las mismas credenciales que la CLI?

**Prueba realizada:**

1. **Credenciales del host:** Docker tiene `credsStore: desktop`; el login se hace con `aws ecr get-login-password | docker login` (mismo token que usa la CLI).
2. **Build con --load:** Se construyó la imagen con `buildx build --load` (la imagen queda en el host).
3. **Push con Docker del host:** Se ejecutó `docker push` (usa las credenciales guardadas en el host tras el login).

**Resultado:** Tras hacer login, `docker push` desde el host devolvió el **mismo 403 Forbidden** en el HEAD request al manifest.

**Conclusión:** Las credenciales que usa buildx son las mismas que las de la CLI y las del `docker push` del host. El 403 no se debe a que buildx use otras credenciales; el mismo usuario obtiene 403 tanto con `buildx --push` como con `docker push`.

---

## 3. Dónde ocurre el 403

El error **no** ocurre en las llamadas que hace la CLI, sino en el **flujo de Docker buildx**:

1. Build de la imagen → OK  
2. Push de layers → OK  
3. Push del manifest (equivalente a `PutImage`) → El log indica "done"  
4. **HEAD de verificación** del manifest → **403 Forbidden**

Ese HEAD es una petición HTTP que hace el cliente Docker (buildx) al registro ECR para comprobar que el manifest se subió. En ECR, esa comprobación suele implicar permisos de **lectura** del manifest (p. ej. `ecr:BatchGetImage` o `ecr:GetDownloadUrlForLayer`), que por CLI **sí están permitidos**.

---

## 4. ¿Es entonces por permisos IAM?

- **Por IAM “puro” (políticas de usuario):**  
  No se puede afirmar que falte un permiso concreto, porque todas las acciones ECR probadas **están permitidas** vía CLI para este usuario en este repo.

- **Sí puede haber algo relacionado con IAM en el flujo real**, por ejemplo:
  - **Contexto de autenticación distinto:** buildx corre en un contenedor y puede usar credenciales o un rol distintos a los de la CLI.
  - **Políticas por recurso/condición:** alguna política podría permitir la misma acción desde la CLI pero denegarla en el contexto del registro (p. ej. según origen de la petición).
  - **Deny explícito** que solo aplique en ciertas condiciones (recurso, IP, etc.) y no en las pruebas que hicimos.

No se pudo usar el simulador de políticas (`iam:SimulatePrincipalPolicy`) porque el usuario no tiene permiso para esa acción.

---

## 5. Permisos que intervienen en la “subida” (push)

Para un push completo a ECR, el flujo típico usa:

| Permiso | Uso en el push | Estado vía CLI |
|---------|----------------|-----------------|
| `ecr:GetAuthorizationToken` | Login al registro | ✅ OK |
| `ecr:BatchCheckLayerAvailability` | Comprobar si las capas ya existen | ✅ OK |
| `ecr:InitiateLayerUpload` | Iniciar subida de capa | ✅ OK |
| `ecr:UploadLayerPart` | Subir trozos de capa | (implícito; layers suben) |
| `ecr:CompleteLayerUpload` | Cerrar subida de capa | (implícito; layers suben) |
| `ecr:PutImage` | Subir el manifest | ❓ No comprobable por CLI; el 403 ocurre después |
| `ecr:BatchGetImage` / lectura | HEAD de verificación del manifest | ✅ OK por CLI |

Es decir: los permisos que **afectan directamente la subida** (upload de layers y manifest) son los de la tabla. Por CLI, los que pudimos probar están permitidos; el que no se puede simular es `PutImage`, y el 403 aparece en el paso de **verificación** (lectura del manifest), no en el upload en sí.

---

## 6. Resumen para el administrador

- **¿Es por permisos IAM?**  
  No se puede asegurar que “falte” un permiso concreto, porque todas las acciones ECR probadas están permitidas por CLI. Sí es posible que haya restricciones IAM que solo apliquen al contexto de Docker/buildx (otro cliente, otro flujo).

- **¿Qué permisos están “afectando” la subida?**  
  En el flujo de push intervienen: `GetAuthorizationToken`, `BatchCheckLayerAvailability`, `InitiateLayerUpload`, `UploadLayerPart`, `CompleteLayerUpload`, `PutImage` y lectura del manifest (p. ej. `BatchGetImage`). Por CLI todos los probados están OK; el 403 solo se ve en el **HEAD de verificación** que hace buildx después del push.

- **Recomendación:**  
  Revisar si el usuario tiene alguna política con **Deny** sobre ECR o sobre este repositorio, y si las políticas que permiten ECR se aplican también a peticiones que vengan del cliente del registro (Docker/buildx), no solo de la CLI. Opcional: probar el mismo push con otro usuario/rol que tenga `AmazonEC2ContainerRegistryPowerUser` (o equivalente) para ver si el 403 desaparece.
