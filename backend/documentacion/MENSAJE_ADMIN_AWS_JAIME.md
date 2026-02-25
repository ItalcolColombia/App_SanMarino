# Mensaje para el administrador AWS - Jaime

**Asunto:** Error 403 al desplegar frontend a ECR - Necesito validar permisos IAM

---

Hola Jaime,

Te escribo porque tenemos un problema al desplegar la aplicación San Marino a AWS. Necesito tu apoyo para validar los permisos de ECR.

## ¿Qué estamos haciendo?

Estamos desplegando la aplicación San Marino (frontend Angular + backend .NET) a AWS ECS. El proceso hace:

1. Login a ECR  
2. Construir la imagen Docker con `docker buildx`  
3. Hacer push de la imagen al repositorio ECR  
4. Actualizar el servicio en ECS  

## El problema

- **Backend:** El push funciona correctamente.  
- **Frontend:** El push falla con **403 Forbidden**.  

## Detalle técnico del error

Al hacer push al repositorio del frontend aparece:

```
ERROR: failed to push 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend:TAG: 
unexpected status from HEAD request to 
https://196080479890.dkr.ecr.us-east-2.amazonaws.com/v2/sanmarino/zootecnia/granjas/frontend/manifests/TAG: 
403 Forbidden
```

El error ocurre después de subir las capas (layers) y el manifest, cuando Docker buildx hace una verificación HEAD del manifest.

## Repositorios ECR

| Componente | Repositorio ECR | Push |
|------------|-----------------|------|
| Backend    | `sanmarino/zootecnia/granjas/backend`    | Funciona |
| Frontend   | `sanmarino/zootecnia/granjas/frontend`   | 403 Forbidden |

**ARN del repositorio frontend:**  
`arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/frontend`

**Usuario afectado:**  
`moisesmurillo@sanmarino.com.co`  
ARN: `arn:aws:iam::196080479890:user/moisesmurillo@sanmarino.com.co`

## Lo que ya validamos

- La política de repositorio ECR del frontend incluye los permisos necesarios para el usuario.
- Backend y frontend tienen la misma configuración de repositorio (sin política en backend, con política en frontend).
- Las operaciones AWS CLI que probamos funcionan: `BatchGetImage`, `InitiateLayerUpload`, `GetAuthorizationToken`.
- El problema persiste solo al hacer push con Docker buildx al repositorio del frontend.

## Lo que necesito que revises

1. **Políticas IAM del usuario `moisesmurillo@sanmarino.com.co`**  
   - Si existen políticas que apliquen solo al repositorio del backend (`.../granjas/backend`).  
   - Si falta incluir el repositorio del frontend (`.../granjas/frontend`).  

2. **Permisos ECR requeridos**  
   El usuario necesita al menos:
   - `ecr:GetAuthorizationToken`
   - `ecr:BatchCheckLayerAvailability`
   - `ecr:GetDownloadUrlForLayer`
   - `ecr:BatchGetImage`
   - `ecr:PutImage`
   - `ecr:InitiateLayerUpload`
   - `ecr:UploadLayerPart`
   - `ecr:CompleteLayerUpload`  

   Sobre estos recursos:
   - `arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/backend`
   - `arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/frontend`

3. **Posibles Deny explícitos**  
   - Si alguna política tiene `Effect: Deny` para ECR o el repositorio del frontend.  

4. **SCP de Organizations**  
   - Si hay políticas de organización que restrinjan acceso a ECR.  

## Política IAM sugerida

En la carpeta `backend/documentacion/` está el archivo `ecr-policy-iam-admin.json` con una política que cubre ambos repositorios. Puedes usarla como referencia o adjuntarla al usuario.

## Resumen

- Backend ECR: push OK.  
- Frontend ECR: push con 403 Forbidden.  
- Repositorio frontend: `sanmarino/zootecnia/granjas/frontend`.  
- Usuario: `moisesmurillo@sanmarino.com.co`.  

Necesito que se validen y ajusten los permisos IAM para que este usuario pueda hacer push también al repositorio del frontend.

Gracias por tu apoyo.
