namespace ZooSanMarino.Application.DTOs;

public class AvesDisponiblesDto
{
    public int HembrasIniciales { get; set; }
    public int MachosIniciales { get; set; }
    public int MortalidadAcumuladaHembras { get; set; }
    public int MortalidadAcumuladaMachos { get; set; }
    public int SeleccionAcumuladaHembras { get; set; }
    public int SeleccionAcumuladaMachos { get; set; }
    public int MortCajaHembras { get; set; }
    public int MortCajaMachos { get; set; }
    public int AsignadasHembras { get; set; }
    public int AsignadasMachos { get; set; }
    public int HembrasDisponibles { get; set; }
    public int MachosDisponibles { get; set; }

    /// <summary>
    /// Total de aves vivas como un único valor MIXTO (= HembrasDisponibles + MachosDisponibles).
    /// Tras el cierre de los reproductora las aves no se devuelven por género: se muestran sumadas.
    /// </summary>
    public int MixtasDisponibles { get; set; }

    /// <summary>
    /// True cuando los lotes reproductora completaron sus 7 días y las aves ya "regresaron"
    /// al lote pollo engorde. En ese caso la UI debe mostrar el total mixto, no el desglose H/M.
    /// </summary>
    public bool AvesDevueltas { get; set; }
}
