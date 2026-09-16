# Plan — PowerPoint ejecutivo ItalGranja

- Fuente: guía de reunión aprobada, 14-sep-2026.
- Entrega: 10 diapositivas editables, 16:9, diagramas operativos y arquitectura, notas para 30 minutos.
- Mensajes: web implementada, ERP pendiente funcional, móvil 80–90 % estimado y sin desplegar.
- Archivos: generador privado en .codex-artifacts/presentacion-italgranja; PPTX final en entregables.
- Sin cambios funcionales, BD o despliegues.
- Validación: integridad PPTX, geometría, revisión visual de las 10 diapositivas y cobertura de la guía.

## Ampliación para cubrir la solicitud de la jefatura

- Añadir agenda visible de 30 minutos, diagrama AWS y diagrama CI/CD. Total: 13 diapositivas.
- Mostrar objetivo y alcance, realizado y pendiente en títulos y contenido, además de notas.
- AWS: ALB enruta web y API a servicios ECS separados; backend conecta RDS. Regiones según documentación del repositorio, sin inventar VPC/subredes.
- CI/CD: push a main-produccion, pruebas y gates, backend antes del frontend, ECR y ECS con espera de estabilidad; OIDC.
- Conservar el archivo anterior y entregar una nueva versión validada.
