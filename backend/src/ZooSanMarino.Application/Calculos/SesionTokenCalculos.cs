// src/ZooSanMarino.Application/Calculos/SesionTokenCalculos.cs
// Cálculo PURO: un solo productor para los dos valores que dependen del `jti` de la sesión
// (el claim que va al JWT y la firma de plataforma que se deriva de él). Sin EF, sin I/O.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Datos de sesión derivados del <c>jti</c>: el claim que se escribe en el JWT y la firma de
/// plataforma (<c>X-Secret-Up</c>) que de él depende. <c>PlatformKey</c> es <c>null</c> cuando no
/// hay <c>DerivationKey</c> configurada (camino legacy).
/// </summary>
public sealed record DatosSesion(string JtiClaim, string? PlatformKey);

/// <summary>
/// Existe para que <c>AuthService</c> no derive el claim <c>jti</c> y la firma de plataforma en dos
/// sitios distintos a partir del mismo <see cref="Guid"/>. Ambos valores salen de <b>una sola</b>
/// llamada, sobre el <b>mismo</b> <c>jti</c> — la fórmula de la firma sigue viviendo, sin duplicar,
/// en <see cref="PlatformSecretCalculos.DerivarClaveSesion"/>.
/// </summary>
public static class SesionTokenCalculos
{
    /// <summary>
    /// <c>JtiClaim</c> = <c>jti.ToString()</c> (formato "D": minúsculas, con guiones — el mismo que
    /// viaja en el claim <c>jti</c> del JWT y el que <c>PlatformSecretCalculos.LeerJtiDeAuthorizationHeader</c>
    /// vuelve a leer en cada request). <c>PlatformKey</c> = <c>DerivarClaveSesion(JtiClaim, derivationKey)</c>.
    /// </summary>
    public static DatosSesion ConstruirDatosDeSesion(Guid jti, string? derivationKey)
    {
        var jtiClaim = jti.ToString();
        var platformKey = PlatformSecretCalculos.DerivarClaveSesion(jtiClaim, derivationKey);
        return new DatosSesion(jtiClaim, platformKey);
    }
}
