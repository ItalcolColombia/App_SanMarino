using System.ComponentModel.DataAnnotations;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// <c>POST /api/Users</c> (21-sep-2026): el body sigue siendo <see cref="RegisterDto"/> y se convierte
/// a <see cref="CreateUserDto"/> para <c>UserService.CreateAsync</c>. Dos contratos:
/// <list type="number">
/// <item>el mapeo no pierde ni inventa datos (granjas siempre vacías, igual que el alta vieja);</item>
/// <item><see cref="RegisterDto"/> conserva la validación de entrada del alta — si alguien cambia el
/// body por <see cref="CreateUserDto"/> (sin atributos), estos tests son los que deben avisar.</item>
/// </list>
/// </summary>
public class AltaUsuarioCalculosTests
{
    private static RegisterDto Registro() => new()
    {
        Email          = "ana.perez@sanmarino.com.co",
        Password       = "Clave2026x",
        SurName        = "Pérez",
        FirstName      = "Ana",
        Cedula         = "1234567",
        Telefono       = "3001234567",
        Ubicacion      = "Granja Norte",
        Zona           = "Zona 1",
        CompanyIds     = [1, 6],
        RoleIds        = [3],
        IsPlatformUser = false
    };

    [Fact]
    public void DesdeRegistro_copia_todos_los_campos()
    {
        var dto = AltaUsuarioCalculos.DesdeRegistro(Registro());

        Assert.Equal("Pérez", dto.SurName);
        Assert.Equal("Ana", dto.FirstName);
        Assert.Equal("1234567", dto.Cedula);
        Assert.Equal("3001234567", dto.Telefono);
        Assert.Equal("Granja Norte", dto.Ubicacion);
        Assert.Equal("ana.perez@sanmarino.com.co", dto.Email);
        Assert.Equal("Clave2026x", dto.Password);
        Assert.Equal(new[] { 1, 6 }, dto.CompanyIds);
        Assert.Equal(new[] { 3 }, dto.RoleIds);
        Assert.Equal("Zona 1", dto.Zona);
        Assert.False(dto.IsPlatformUser);
    }

    [Fact]
    public void DesdeRegistro_nunca_asigna_granjas()
    {
        // RegisterDto no las trae: se asignan después con POST /api/Users/{id}/farms.
        Assert.Empty(AltaUsuarioCalculos.DesdeRegistro(Registro()).FarmIds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DesdeRegistro_respeta_usuario_de_plataforma(bool esPlataforma)
    {
        var registro = Registro();
        registro.IsPlatformUser = esPlataforma;

        Assert.Equal(esPlataforma, AltaUsuarioCalculos.DesdeRegistro(registro).IsPlatformUser);
    }

    [Fact]
    public void DesdeRegistro_roles_ausentes_salen_vacios_no_null()
    {
        var registro = Registro();
        registro.RoleIds = null;

        // CreateAsync decide qué hacer con "sin roles"; el mapeo no debe hacerlo explotar con null.
        Assert.Empty(AltaUsuarioCalculos.DesdeRegistro(registro).RoleIds);
    }

    [Fact]
    public void DesdeRegistro_zona_nula_se_conserva()
    {
        var registro = Registro();
        registro.Zona = null;

        Assert.Null(AltaUsuarioCalculos.DesdeRegistro(registro).Zona);
    }

    // ── Validación de entrada que el endpoint hereda de RegisterDto ────────────────────────────

    private static List<ValidationResult> Validar(RegisterDto dto)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), errores, validateAllProperties: true);
        return errores;
    }

    [Fact]
    public void RegisterDto_valido_no_tiene_errores()
    {
        Assert.Empty(Validar(Registro()));
    }

    [Theory]
    [InlineData("abc123")]      // 6 caracteres: CreateAsync lo aceptaría, el alta NO
    [InlineData("soloLetras")]  // sin número
    [InlineData("12345678")]    // sin letra
    public void RegisterDto_rechaza_contrasena_debil(string password)
    {
        var registro = Registro();
        registro.Password = password;

        Assert.Contains(Validar(registro), e => e.MemberNames.Contains(nameof(RegisterDto.Password)));
    }

    [Fact]
    public void RegisterDto_rechaza_email_sin_formato()
    {
        var registro = Registro();
        registro.Email = "no-es-un-correo";

        Assert.Contains(Validar(registro), e => e.MemberNames.Contains(nameof(RegisterDto.Email)));
    }
}
