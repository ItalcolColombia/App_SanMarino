# Detalle de acciones realizadas para solucionar el error 403 en ECR Frontend

---

## 1. Login explícito a ECR antes del deploy

- **Acción:** Ejecutar `aws ecr get-login-password --region us-east-2 | docker login --username AWS --password-stdin 196080479890.dkr.ecr.us-east-2.amazonaws.com` antes de `make deploy-frontend`.
- **Objetivo:** Renovar el token de ECR por si había expirado.
- **Resultado:** El login funciona, pero el 403 persiste.

---

## 2. Modificación del script de despliegue (buildx vs docker push)

- **Acción:** Cambiar el flujo de push:
  - Primera versión: `buildx build --push` (igual que backend).
  - Luego: `buildx build --load` + `docker push` para usar credenciales del host.
- **Objetivo:** Evitar posibles problemas de credenciales dentro del contenedor de buildx.
- **Resultado:** El 403 aparece tanto con buildx --push como con docker push.

---

## 3. Alineación del script frontend con el backend

- **Acción:** Ajustar el script para que use el mismo flujo que el backend:
  - Login con ECR_URI completa.
  - Comando `buildx build --platform linux/amd64 --push`.
- **Objetivo:** Garantizar consistencia entre backend (funciona) y frontend (falla).
- **Resultado:** El frontend sigue fallando con 403 a pesar de la misma lógica.

---

## 4. Eliminación de la política del repositorio ECR frontend

- **Acción:** Ejecutar `aws ecr delete-repository-policy --repository-name sanmarino/zootecnia/granjas/frontend --region us-east-2`.
- **Objetivo:** Igualar la configuración al backend, que no tiene política y funciona correctamente.
- **Resultado:** El 403 continúa. Backend y frontend quedan ambos sin política de repositorio.

---

## 5. Eliminación del archivo ecr-repository-policy.json

- **Acción:** Borrar el archivo `frontend/deploy/ecr-repository-policy.json`.
- **Objetivo:** Limpiar referencias a políticas que ya no se usan.
- **Resultado:** Solo limpieza; no afecta al 403.

---

## 6. Aplicación de política de repositorio con account root

- **Acción:** Crear y aplicar una política de repositorio con Principal `arn:aws:iam::196080479890:root`.
- **Objetivo:** Permitir explícitamente operaciones de push/pull desde la cuenta.
- **Resultado:** El 403 persiste.

---

## 7. Aplicación de política de repositorio para el usuario específico

- **Acción:** Crear `frontend/deploy/ecr-policy-frontend.json` con Principal `arn:aws:iam::196080479890:user/moisesmurillo@sanmarino.com.co` y aplicar con `aws ecr set-repository-policy`.
- **Objetivo:** Otorgar permisos ECR directamente al usuario en el repositorio del frontend.
- **Resultado:** El 403 continúa.

---

## 8. Build sin caché (--no-cache)

- **Acción:** Ejecutar `docker buildx build --no-cache ... --push` para construir la imagen desde cero.
- **Objetivo:** Descartar que el problema fuera de caché o contenido de la imagen.
- **Resultado:** Build correcto, pero el mismo 403 en el push.

---

## 9. Validación de permisos y estado en AWS

- **Acciones realizadas:**
  - Comprobar existencia de los repos backend y frontend.
  - Comparar configuraciones (encryption, scanOnPush).
  - Probar `ecr:DescribeRepositories`, `ecr:GetAuthorizationToken`.
  - Probar `ecr:BatchGetImage` sobre el frontend (funciona).
  - Probar `ecr:InitiateLayerUpload` sobre el frontend (funciona).
  - Verificar que la imagen no llegaba a ECR tras el fallo (`DescribeImages` con ImageNotFoundException).
- **Resultado:** Las operaciones de lectura y escritura básicas funcionan; el 403 solo aparece en el HEAD de verificación del manifest tras el push.

---

## 10. Intentos de consulta IAM

- **Acciones:**
  - `iam:ListAttachedUserPolicies` → AccessDenied.
  - `iam:ListUserPolicies` → AccessDenied.
  - `iam:ListGroupsForUser` → AccessDenied.
  - `iam:SimulatePrincipalPolicy` → AccessDenied.
- **Resultado:** No se pudo inspeccionar ni validar las políticas IAM del usuario desde esta cuenta.

---

## 11. Documentación creada para el administrador AWS

- **Archivos generados:**
  - `VALIDACION_PERMISOS_AWS_ECR_ECS.md`: resumen de permisos validados.
  - `ANALISIS_ERROR_403_ECR_FRONTEND.md`: análisis técnico del error.
  - `REQUISITOS_IAM_ECR_ADMIN.md`: instrucciones y comandos para el admin.
  - `ecr-policy-iam-admin.json`: política IAM sugerida.
  - `MENSAJE_ADMIN_AWS_JAIME.md`: mensaje para Jaime con el problema y validaciones requeridas.

---

## Resumen

| # | Acción | Resultado |
|---|--------|-----------|
| 1 | Login explícito a ECR | No resolvió |
| 2 | Cambio buildx / docker push | No resolvió |
| 3 | Alineación con script backend | No resolvió |
| 4 | Eliminar política repo frontend | No resolvió |
| 5 | Eliminar archivo ecr-repository-policy.json | Limpieza |
| 6 | Política repo con account root | No resolvió |
| 7 | Política repo para usuario moisesmurillo | No resolvió |
| 8 | Build sin caché | No resolvió |
| 9 | Validación de permisos AWS | Operaciones OK; 403 solo en HEAD post-push |
| 10 | Consulta políticas IAM | AccessDenied |
| 11 | Documentación para admin | Completada |

**Conclusión:** El problema sigue apuntando a restricciones a nivel IAM sobre el repositorio frontend. Se requiere intervención del administrador AWS para revisar y ajustar las políticas del usuario.
