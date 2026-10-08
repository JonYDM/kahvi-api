using Chiron.Application.Categorias;
using Chiron.Application.Comandas;
using Chiron.Application.PuntoVenta;
using Chiron.Application.Seguridad;
using Chiron.Domain.Usuarios;
using Chiron.Infrastructure.Mensajeria;
using Chiron.Infrastructure.Persistencia;
using Chiron.Infrastructure.Seguridad;
using Microsoft.Extensions.DependencyInjection;

namespace Chiron.Infrastructure;

/// <summary>
/// Registro de dependencias de la capa de Infraestructura.
/// Modo único: persistencia EN MEMORIA (demo/desarrollo sin base de datos).
/// El segundo parámetro (jwtOpciones) configura la firma de tokens; si es null se usa
/// una configuración por defecto (solo apta para pruebas locales).
/// </summary>
public static class DependencyInjection
{
    /// <summary>Persistencia EN MEMORIA para demo y desarrollo.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, JwtOpciones? jwtOpciones = null)
    {
        // Repositorio genérico: cubre Cafeteria, Sucursal, PagoSuscripcion y cualquier otra entidad base.
        services.AddSingleton(typeof(Chiron.Application.Common.IRepository<>), typeof(RepositorioEnMemoria<>));

        // Repositorio de categorías dinámicas del menú.
        services.AddSingleton<CategoriaRepositorioEnMemoria>();
        services.AddSingleton<ICategoriaRepository>(sp => sp.GetRequiredService<CategoriaRepositorioEnMemoria>());

        // Repositorios específicos del punto de venta.
        services.AddSingleton<ProductoRepositorioEnMemoria>();
        services.AddSingleton<IProductoRepository>(sp => sp.GetRequiredService<ProductoRepositorioEnMemoria>());

        services.AddSingleton<VentaRepositorioEnMemoria>();
        services.AddSingleton<IVentaRepository>(sp => sp.GetRequiredService<VentaRepositorioEnMemoria>());

        // Repositorio de usuarios.
        services.AddSingleton<UsuarioRepositorioEnMemoria>();
        services.AddSingleton<IUsuarioRepository>(sp => sp.GetRequiredService<UsuarioRepositorioEnMemoria>());

        // Repositorio de comandas.
        services.AddSingleton<ComandaRepositorioEnMemoria>();
        services.AddSingleton<IComandaRepository>(sp => sp.GetRequiredService<ComandaRepositorioEnMemoria>());

        AddMensajeria(services);
        AddSeguridad(services, jwtOpciones);
        return services;
    }

    // Servicio de mensajería: implementación de consola para demo/pruebas.
    private static void AddMensajeria(IServiceCollection services)
        => services.AddSingleton<MensajeriaConsola>();

    // Servicios de seguridad: hasheo de contraseñas y generación de JWT.
    private static void AddSeguridad(IServiceCollection services, JwtOpciones? jwtOpciones)
    {
        services.AddSingleton<IHasheadorContrasena, HasheadorBCrypt>();

        // Clave por defecto SOLO para pruebas locales; en producción viene de configuración.
        var opciones = jwtOpciones ?? new JwtOpciones
        {
            Clave = "clave-de-desarrollo-solo-local-cambiar-en-produccion-1234567890"
        };
        services.AddSingleton(opciones);
        services.AddSingleton<IGeneradorToken, GeneradorTokenJwt>();
    }
}
