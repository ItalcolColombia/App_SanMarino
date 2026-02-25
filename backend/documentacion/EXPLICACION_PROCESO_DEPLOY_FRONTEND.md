# Explicación: qué está pasando en el despliegue del frontend

**Para el administrador:** por qué en ECR solo se ve la imagen vieja y ECS no usa una imagen nueva.

---

## En qué consiste el proceso (paso a paso)

1. **Construir la imagen**  
   En mi máquina se construye una imagen Docker del frontend (Angular + nginx). Eso termina bien.

2. **Subir la imagen a ECR**  
   Esa imagen se debe **subir (push)** al repositorio de Amazon ECR:  
   `sanmarino/zootecnia/granjas/frontend`  
   Aquí es donde **falla**: al intentar subir, Amazon responde **403 Forbidden**.  
   Cuando hay 403, **la imagen no se guarda en ECR**. No se escribe nada nuevo en el repositorio.

3. **Actualizar ECS**  
   Solo si el paso 2 termina bien, el script registra una nueva Task Definition en ECS y actualiza el servicio para que use la nueva imagen.  
   Como el paso 2 falla, **nunca llegamos a este paso**. El script se detiene en el 403.

4. **Qué usa ECS entonces**  
   ECS sigue usando la última imagen que **sí** llegó a ECR. Esa es la del **26 de enero de 2026** (tag `20260126-2022`). Por eso en el repo solo ves esa y `latest` (que apunta a esa misma).

---

## Dónde se “corta” el flujo

```
[Mi máquina]                    [AWS ECR]                      [AWS ECS]
     |                               |                              |
     |  1. Build imagen (OK)         |                              |
     |  -------------------------->  |                              |
     |                               |                              |
     |  2. Push imagen               |                              |
     |  -------------------------->  |  403 Forbidden               |
     |  <--------------------------  |  (no se guarda la imagen)    |
     |     FALLO AQUÍ                |                              |
     |                               |                              |
     |  (el script se detiene)       |  Repo sigue con la           |
     |                               |  imagen anterior             |
     |                               |  (20260126-2022, latest)     |
     |                               |                              |
     |  3. Actualizar ECS            |                              |
     |  (nunca se ejecuta)            |  --------------------------> |  Sigue usando
     |                               |                              |  la imagen vieja
```

La imagen nueva **no está guardada en ECR** porque el **push es rechazado (403)**. Por eso:

- En ECR solo ves la imagen anterior: `20260126-2022` y `latest`.
- ECS no puede usar una imagen “nueva” que no existe en ECR; por eso sigue usando esa misma imagen vieja.

---

## Resumen en una frase

**La imagen nueva no se está guardando en ECR porque el push recibe 403 Forbidden; por eso en el repo solo aparece la última que sí se subió (26-ene-2026) y ECS sigue usando esa.**

---

## Qué tendría que pasar para que se vea la imagen nueva

1. El **push a ECR** tiene que completarse sin 403 (que AWS acepte guardar la nueva imagen en el repo).
2. Solo entonces el script sigue y **actualiza ECS** para que use esa nueva imagen.
3. Entonces en ECR verías un tag nuevo (por ejemplo `20260224-XXXX`) y ECS desplegaría esa versión.

Hoy el punto 1 falla, por eso nunca se guarda la imagen nueva y nunca se actualiza ECS.
