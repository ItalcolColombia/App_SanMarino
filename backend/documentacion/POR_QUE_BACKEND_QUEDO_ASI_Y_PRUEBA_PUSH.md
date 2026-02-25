# Por qué el backend quedó con TD 40/41 apuntando a imagen inexistente

## Qué pasó

1. **El push a ECR del backend también falla con 403** (igual que el frontend), en el paso de verificación HEAD del manifest. Se comprobó con un push manual: mismo error 403.
2. El script de deploy del backend **consideraba éxito** si en la salida aparecía la frase "pushing manifest", pero **no comprobaba el código de salida** de `docker buildx build`. Como "pushing manifest" sale antes del ERROR 403, el script seguía y registraba la Task Definition y actualizaba el servicio **aunque la imagen no se hubiera guardado en ECR**.
3. Por eso existen TD 40 y 41 con tags `20260224-1542` y `20260224-1555`: el script creyó que el push había ido bien, registró la TD y actualizó el servicio, pero la imagen **nunca llegó a ECR** (403).
4. ECS intenta desplegar la TD nueva, no encuentra la imagen en ECR y no arranca tareas. La TD 35 (imagen `20260126-1959`) sigue activa y es la que corre.

**Resumen:** Backend y frontend sufren el mismo 403 al hacer push a ECR. El script del backend tenía un bug que reportaba éxito cuando en realidad el push fallaba; por eso hay TDs apuntando a imágenes que no existen.

---

## Resultado de la prueba (push manual del backend)

Se ejecutó un push manual del backend a ECR (tag de prueba). **El push también falló con 403 Forbidden** en el mismo paso (HEAD request al manifest). Por tanto:

- **Backend y frontend están afectados por el mismo problema** (403 al subir a ECR).
- La última imagen del backend que **sí** está en ECR es la del **26-ene-2026** (tag `20260126-1959`); los intentos posteriores no guardan la imagen por el 403.
- El script del backend se corrigió para que no dé éxito cuando `docker buildx build` falle: ahora usa el código de salida del comando en lugar de buscar "pushing manifest" en la salida.

---

## Prueba: validar que el push del backend sigue funcionando

Para comprobar que el flujo de **push del backend a ECR** está activo y funciona:

1. Ejecutar un deploy completo del backend: `make deploy-backend`.
2. Verificar que:
   - El push a ECR termina sin error.
   - La nueva imagen aparece en ECR con el tag del momento.
   - ECS actualiza el servicio y las tareas usan la nueva imagen.

Si todo eso ocurre, el "servicio" de push del back está activo; si falla, el error indicará dónde (ECR, red, permisos, etc.).
