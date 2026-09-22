// src/ZooSanMarino.Application/Calculos/AltaUsuarioCalculos.cs
// Mapeo PURO del alta de usuario de la pantalla de Usuarios. Sin EF, sin estado, sin I/O.
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// <c>POST /api/Users</c> recibe <see cref="RegisterDto"/> y lo crea con <c>IUserService.CreateAsync</c>.
///
/// <para>
/// <b>Por qué el body sigue siendo <see cref="RegisterDto"/> (21-sep-2026).</b> Hasta acá el endpoint
/// llamaba a <c>IAuthService.RegisterAsync</c> —el camino del login—, así que respondía forma de sesión
/// (<c>token</c>, <c>platformKey</c>, <c>menu</c>…) y registraba una sesión en <c>sesiones_activas</c>
/// para alguien que nunca inició sesión. Ahora usa <c>CreateAsync</c>, que responde <see cref="UserDto"/>.
/// Pero la validación de ENTRADA del alta vive en los atributos de <see cref="RegisterDto"/> (contraseña
/// ≥ 8 con letra y número, formato de email, anti-inyección, largos máximos) y <see cref="CreateUserDto"/>
/// no tiene ninguno: cambiar el tipo del body la habría debilitado. Por eso solo cambia la salida.
/// Ver <c>fase_de_desarrollo/users_create_endpoint_userdto_plan.md</c>.
/// </para>
/// </summary>
public static class AltaUsuarioCalculos
{
    /// <summary>
    /// Convierte el body ya validado. <see cref="RegisterDto"/> no trae granjas (se asignan después con
    /// <c>POST /api/Users/{id}/farms</c>), así que <see cref="CreateUserDto.FarmIds"/> sale vacío: el
    /// mismo resultado que el alta vieja, que las ignoraba.
    /// </summary>
    public static CreateUserDto DesdeRegistro(RegisterDto dto) => new(
        SurName:        dto.SurName,
        FirstName:      dto.FirstName,
        Cedula:         dto.Cedula,
        Telefono:       dto.Telefono,
        Ubicacion:      dto.Ubicacion,
        Email:          dto.Email,
        Password:       dto.Password,
        CompanyIds:     dto.CompanyIds ?? Array.Empty<int>(),
        RoleIds:        dto.RoleIds ?? Array.Empty<int>(),
        FarmIds:        Array.Empty<int>(),
        Zona:           dto.Zona,
        IsPlatformUser: dto.IsPlatformUser
    );
}
