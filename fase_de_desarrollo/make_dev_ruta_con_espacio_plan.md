# `make dev` no levanta nada — ruta del repo con espacio (22-sep-2026)

## Síntoma

`make dev` abre dos ventanas de PowerShell y ninguna levanta el servicio: ni :5002 (back) ni :4200
(front) quedan escuchando. `make dev-back` y `make dev-front` por separado sí funcionan.

## Causa raíz (reproducida)

`dev.ps1` lanza cada servicio con:

```powershell
Start-Process powershell -ArgumentList '-NoExit','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'dev-back.ps1')
```

En Windows PowerShell 5.1, `Start-Process` **une los elementos de `-ArgumentList` con espacios y no
les pone comillas**. La ruta del repo es `C:\Users\SAN MARINO\Desktop\App_SanMarino\...`, así que la
ventana hija recibe `-File C:\Users\SAN` + `MARINO\Desktop\...` como argumentos sueltos → PowerShell
no encuentra el archivo y, por el `-NoExit`, la ventana queda abierta mostrando solo el error.

Reproducido con un script de prueba en una carpeta con espacio:
- sin comillas → exit `-196608`, el script no corre;
- con la ruta entre `"..."` → exit `0`, el script corre.

Los scripts individuales se verificaron sueltos: `dev-back.ps1` toma el dotnet 10.0.301 y
`dev-front.ps1` el Node 22.23.1 portable — el toolchain no es el problema.

Hallazgo secundario: `make dev-back` cierra la API anterior con `dev-kill-back.cmd` antes de
compilar (si no, el `bin/` queda bloqueado con MSB3027), pero `make dev` no lo hacía.

## Cambios

| Archivo | Cambio |
|---|---|
| `dev.ps1` | Pasar la ruta de cada script **entre comillas dobles** a `-File`. ASCII puro (PS 5.1). Sin lógica de cerrar procesos (AMSI bloquea el `.ps1` entero). |
| `Makefile` | Target `dev`: correr `dev-kill-back.cmd` antes del `.ps1`, igual que `dev-back`. |

Sin cambios de BD, backend ni frontend. No toca contratos ni lógica de negocio.

## Casos de prueba

1. `make dev` desde PowerShell → abre 2 ventanas; :5002 responde (Swagger/401) y :4200 responde 200.
2. `make dev` con una API anterior viva → la cierra antes de compilar (no MSB3027).
3. `dev.ps1` sigue siendo ASCII puro (0 caracteres no-ASCII).
4. Al terminar la verificación: cerrar back y front y confirmar :5002 y :4200 libres.
