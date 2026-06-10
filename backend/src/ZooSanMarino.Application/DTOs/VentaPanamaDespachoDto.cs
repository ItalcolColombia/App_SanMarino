namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Línea de venta Panamá: el usuario asigna H/M SOBRE las mixtas del lote. El stock se descuenta de
/// las mixtas (H+M); el registro guarda el split en hembras/machos para el reporte.
/// </summary>
public sealed class VentaPanamaDespachoLineaDto
{
    public int LoteAveEngordeOrigenId { get; set; }
    public int? GranjaOrigenId { get; set; }
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }
    /// <summary>Aves a vender como hembras, asignadas sobre las mixtas del lote.</summary>
    public int CantidadHembras { get; set; }
    /// <summary>Aves a vender como machos, asignadas sobre las mixtas del lote.</summary>
    public int CantidadMachos { get; set; }
}

/// <summary>
/// Cabecera de despacho compartida + líneas por lote de un galpón (venta Panamá). Crea N movimientos
/// Pendiente (uno por lote, <c>EsVentaMixta=true</c>) con la misma factura/despacho, en una transacción.
/// </summary>
public sealed class CreateVentaPanamaDespachoDto
{
    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
    public string TipoMovimiento { get; set; } = "Venta";
    public int? GranjaOrigenId { get; set; }
    public int UsuarioMovimientoId { get; set; }
    public string? MotivoMovimiento { get; set; }
    public string? Descripcion { get; set; }
    public string? Observaciones { get; set; }
    public string? NumeroDespacho { get; set; }
    public int? EdadAves { get; set; }
    /// <summary>El cliente puede enviar decimal; se redondea al guardar (columna entera).</summary>
    public double? TotalPollosGalpon { get; set; }
    public string? Raza { get; set; }
    public string? Placa { get; set; }
    public TimeOnly? HoraSalida { get; set; }
    public string? GuiaAgrocalidad { get; set; }
    public string? Sellos { get; set; }
    public string? Ayuno { get; set; }
    public string? Conductor { get; set; }
    public double? PesoBruto { get; set; }
    public double? PesoTara { get; set; }
    public List<VentaPanamaDespachoLineaDto> Lineas { get; set; } = new();
}
