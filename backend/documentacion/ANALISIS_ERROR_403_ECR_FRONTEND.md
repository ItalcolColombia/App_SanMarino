# Análisis Error 403 - Push ECR Frontend

**Fecha:** 24-Feb-2026  
**Repositorio:** sanmarino/zootecnia/granjas/frontend

---

## 1. TIPO DE ERROR

| Campo | Valor |
|-------|-------|
| **Código HTTP** | 403 Forbidden |
| **Operación fallida** | HEAD request a `/v2/.../manifests/{TAG}` |
| **Momento** | Después de "pushing manifest... done" (paso de verificación de buildx) |
| **¿La imagen llegó a ECR?** | **No** — ImageNotFoundException al consultar el tag |

---

## 2. SECUENCIA DEL DEPLOY

```
1. Login ECR          → OK
2. Build imagen       → OK (capa usa cache)
3. Push layers        → OK (4.4s)
4. Push manifest      → Log dice "done" (0.3s)
5. HEAD verificación  → 403 Forbidden
6. Script falla       → Imagen no se registra en ECR
```

---

## 3. ¿SE NECESITA RECONSTRUIR LA IMAGEN?

**No.** El build termina correctamente:

- El Dockerfile es válido
- Todas las capas se construyen (o usan cache)
- La imagen se exporta correctamente
- El problema está en la fase de push/verificación a ECR, no en el build

---

## 4. CAUSA PROBABLE

El 403 ocurre cuando **Docker buildx** hace un **HEAD** sobre el manifest tras el push. Posibles motivos:

1. **IAM insuficiente para lectura posterior al push**  
   El usuario puede tener `PutImage` pero no `BatchGetImage` o permisos equivalentes para el HEAD que usa buildx al validar el manifest.

2. **Credenciales dentro del builder buildx**  
   buildx usa el driver `docker-container` (builder "multiarch"), que corre en un contenedor. Las credenciales del host pueden no aplicarse igual para el paso de verificación.

3. **Restricción por repositorio**  
   Políticas IAM o SCP que permiten el backend pero no el frontend.

---

## 5. ARCHIVOS IMPLICADOS

| Archivo | Estado |
|---------|--------|
| `frontend/scripts/deploy-frontend-ecs.sh` | OK — flujo alineado con backend |
| `frontend/Dockerfile` | OK — build correcto |
| `frontend/deploy/ecs-taskdef.json` | OK — usado en pasos posteriores |
| `frontend/deploy/ecr-repository-policy.json` | Eliminado (ya no se usa) |

---

## 6. DIFERENCIA CON BACKEND

| Aspecto | Backend | Frontend |
|---------|---------|----------|
| Build | buildx --push | buildx --push |
| Login | ECR_URI completa | ECR_URI completa |
| Repo ECR | Sin política | Sin política |
| Resultado | Push OK | 403 en HEAD |

La configuración es equivalente. La diferencia sugiere permisos o políticas IAM/SCP específicas del repositorio frontend.

---

## 7. ACCIONES SUGERIDAS

1. **Revisar IAM con el administrador**  
   Asegurar que el usuario tenga, al menos:
   - `ecr:PutImage`
   - `ecr:BatchGetImage`
   - `ecr:GetDownloadUrlForLayer`
   - `ecr:BatchCheckLayerAvailability`
   
   sobre `arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/*`.

2. **Probar desde EC2/CodeBuild en la misma cuenta**  
   Confirmar si el error se reproduce con otro contexto (por ejemplo, rol de EC2/CodeBuild con permisos ECR).

3. **Probar build + push en dos pasos**  
   - Build: `docker buildx build --load -t ...`
   - Push manual: `docker push ...`  
   Para comprobar si el 403 es exclusivo de buildx.

4. **Contactar con soporte AWS**  
   Si las políticas parecen correctas, solicitar revisión del error 403 en HEAD de manifest en ECR.
