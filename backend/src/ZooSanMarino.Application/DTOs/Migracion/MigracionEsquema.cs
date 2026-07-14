// src/ZooSanMarino.Application/DTOs/Migracion/MigracionEsquema.cs
namespace ZooSanMarino.Application.DTOs.Migracion;

/// <summary>
/// Definición de una columna de la plantilla de un tipo de migración: fuente única compartida por
/// la generación de la plantilla .xlsx y la validación de encabezados del archivo subido.
/// </summary>
/// <param name="Titulo">Encabezado canónico tal como aparece en la plantilla generada.</param>
/// <param name="Requerida">Si la COLUMNA debe existir en el archivo (valor obligatorio por fila).</param>
/// <param name="Alias">Claves normalizadas alternativas que el parseo acepta además del título (las que hoy reciben las llamadas <c>Celda(...)</c>). La clave normalizada del propio título se acepta siempre.</param>
/// <param name="Opciones">Valores de un dropdown inline (ej. Estado "A"/"I"); null si la columna no tiene lista de opciones fija.</param>
public sealed record ColumnaEsquema(string Titulo, bool Requerida, string[]? Alias = null, string[]? Opciones = null);

/// <summary>Esquema completo de un tipo de migración: hoja de datos, columnas (en orden) y tope de filas.</summary>
/// <param name="Hoja">Nombre de la hoja de datos en el Excel (hoy siempre "Datos").</param>
/// <param name="Columnas">Columnas de la plantilla, en el orden en que se generan.</param>
/// <param name="MaxFilas">Tope de filas de datos admitidas por archivo (protección de memoria).</param>
public sealed record EsquemaMigracion(string Hoja, IReadOnlyList<ColumnaEsquema> Columnas, int MaxFilas = 5000);
