// src/ZooSanMarino.Application/Calculos/TicketResolutorAlcanceCalculos.cs
// Regla PURA del alcance de un resolutor de tickets (EMPRESA / GLOBAL).
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Quién ATIENDE los tickets de una empresa y cómo se guarda la plantilla de atención (de un rol o de
/// una persona) sin que un cambio de UNA empresa pise a las demás.
/// </summary>
/// <remarks>
/// <para>
/// Alcance <b>EMPRESA</b>: la fila atiende solo los tickets de su <c>company_id</c>. Alcance
/// <b>GLOBAL</b>: atiende los de todas las empresas (y las que se creen). Ver
/// <see cref="TicketAlcance"/>.
/// </para>
/// <para>
/// Una sola fórmula: <see cref="Aplica"/> la usan el desplegable «Asignar a», la validación al crear y
/// al transferir, y la visibilidad del ticket. Antes eran tres implementaciones con reglas distintas
/// (el desplegable filtraba por empresa y la validación de <c>CreateAsync</c> no).
/// </para>
/// </remarks>
public static class TicketResolutorAlcanceCalculos
{
    /// <summary>¿Una fila de resolutor atiende los tickets de <paramref name="empresaTicket"/>?</summary>
    public static bool Aplica(string? alcance, int filaCompanyId, int empresaTicket) =>
        TicketAlcance.Normalizar(alcance) == TicketAlcance.Global || filaCompanyId == empresaTicket;

    /// <summary>
    /// Etiqueta que acompaña al resolutor en «Asignar a». «Global» SOLO para alcance GLOBAL; una fila
    /// de empresa dice de qué empresa es. Antes decía «Global» toda fila con <c>pais_id NULL</c>
    /// (casi todas), y así un resolutor solo de Sanmarino se leía como global.
    /// </summary>
    public static string Etiqueta(string? alcance, string? nombreEmpresa) =>
        TicketAlcance.Normalizar(alcance) == TicketAlcance.Global
            ? "Global"
            : string.IsNullOrWhiteSpace(nombreEmpresa) ? "Empresa" : nombreEmpresa.Trim();

    // ───────────────────────── Plan de guardado de una plantilla ─────────────────────────

    /// <summary>Fila existente de la plantilla (de un rol o de un usuario).</summary>
    public sealed record Fila(long Id, string Tipo, int? PaisId, int CompanyId, string Alcance, bool Activo);

    /// <summary>
    /// Ítem pedido por la pantalla. <paramref name="Alcance"/> null = «no lo dice»: se conserva el de
    /// la fila existente (pantallas viejas que no conocen el alcance reenvían lo que cargaron).
    /// </summary>
    public sealed record Pedido(string Tipo, int? PaisId, string? Alcance);

    /// <summary>Cambio sobre una fila existente.</summary>
    public sealed record Cambio(long Id, bool Activo, string Alcance);

    /// <summary>Fila a crear (su <c>company_id</c> es la empresa del plan).</summary>
    public sealed record Alta(string Tipo, int? PaisId, string Alcance);

    /// <summary>
    /// Resultado del plan. <see cref="TocaGlobal"/> = el plan activa, apaga, crea o convierte alguna
    /// fila GLOBAL: solo lo puede ejecutar el admin global.
    /// </summary>
    public sealed record Plan(IReadOnlyList<Cambio> Cambios, IReadOnlyList<Alta> Altas, bool TocaGlobal);

    /// <summary>
    /// Calcula qué hay que cambiar para que la plantilla, vista desde <paramref name="empresa"/>, quede
    /// exactamente como <paramref name="pedido"/>.
    /// </summary>
    /// <param name="existentes">
    /// Filas de la plantilla visibles desde la empresa: las EMPRESA con <c>company_id = empresa</c> y
    /// las GLOBAL de cualquier empresa. Las EMPRESA de otras empresas NO se pasan: no se tocan.
    /// </param>
    /// <param name="pedido">Tipos que tienen que quedar activos (tipos inválidos ya filtrados).</param>
    /// <param name="empresa">Empresa donde vive la plantilla (ver <see cref="TicketPerfilEmpresaCalculos"/>).</param>
    /// <remarks>
    /// El índice único es (entidad, tipo, país, empresa) sin el alcance: en una empresa hay a lo sumo
    /// UNA fila por (tipo, país). Por eso pasar un tipo de EMPRESA a GLOBAL (o al revés) convierte la
    /// fila en su lugar en vez de crear otra.
    /// </remarks>
    public static Plan Planificar(IReadOnlyList<Fila> existentes, IEnumerable<Pedido> pedido, int empresa)
    {
        var filas = existentes.ToDictionary(f => f.Id);
        var estadoFinal = existentes.ToDictionary(f => f.Id, f => (f.Activo, Alcance: TicketAlcance.Normalizar(f.Alcance)));
        var reclamadas = new HashSet<long>();
        var altas = new List<Alta>();
        var altasVistas = new HashSet<(string, int?)>();

        bool Mismo(Fila f, string tipo, int? pais) =>
            string.Equals(f.Tipo, tipo, StringComparison.OrdinalIgnoreCase) && f.PaisId == pais;

        foreach (var p in pedido)
        {
            var tipo = p.Tipo.Trim().ToUpperInvariant();
            if (!altasVistas.Add((tipo, p.PaisId))) continue; // pedido repetido: una sola vez

            var local = existentes.FirstOrDefault(f => f.CompanyId == empresa && Mismo(f, tipo, p.PaisId));
            var globales = existentes
                .Where(f => TicketAlcance.Normalizar(f.Alcance) == TicketAlcance.Global && Mismo(f, tipo, p.PaisId))
                .ToList();

            var alcance = p.Alcance is null
                ? (globales.Any(g => g.Activo) ? TicketAlcance.Global
                   : local is not null ? TicketAlcance.Normalizar(local.Alcance)
                   : globales.Count > 0 ? TicketAlcance.Global
                   : TicketAlcance.Empresa)
                : TicketAlcance.Normalizar(p.Alcance);

            if (alcance == TicketAlcance.Global)
            {
                if (globales.Count > 0)
                {
                    foreach (var g in globales) { estadoFinal[g.Id] = (true, TicketAlcance.Global); reclamadas.Add(g.Id); }
                }
                else if (local is not null)
                {
                    estadoFinal[local.Id] = (true, TicketAlcance.Global); reclamadas.Add(local.Id);
                }
                else altas.Add(new Alta(tipo, p.PaisId, TicketAlcance.Global));
            }
            else
            {
                if (local is not null) { estadoFinal[local.Id] = (true, TicketAlcance.Empresa); reclamadas.Add(local.Id); }
                else altas.Add(new Alta(tipo, p.PaisId, TicketAlcance.Empresa));
                // Las GLOBAL del mismo tipo que NO son la fila local quedan sin reclamar ⇒ se apagan:
                // «atiende esta empresa» reemplaza a «atiende todas».
            }
        }

        foreach (var f in existentes)
        {
            if (reclamadas.Contains(f.Id)) continue;
            estadoFinal[f.Id] = (false, estadoFinal[f.Id].Alcance);
        }

        var cambios = new List<Cambio>();
        var tocaGlobal = altas.Any(a => a.Alcance == TicketAlcance.Global);
        foreach (var (id, fin) in estadoFinal)
        {
            var antes = filas[id];
            var alcanceAntes = TicketAlcance.Normalizar(antes.Alcance);
            if (antes.Activo == fin.Activo && alcanceAntes == fin.Alcance) continue;
            cambios.Add(new Cambio(id, fin.Activo, fin.Alcance));
            if (alcanceAntes == TicketAlcance.Global || fin.Alcance == TicketAlcance.Global) tocaGlobal = true;
        }

        return new Plan(cambios.OrderBy(c => c.Id).ToList(), altas, tocaGlobal);
    }
}
