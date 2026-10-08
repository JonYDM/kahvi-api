using Microsoft.Extensions.Logging;

namespace Chiron.Infrastructure.Mensajeria;

/// <summary>
/// Contrato para el servicio de mensajería (notificaciones por WhatsApp u otro canal).
/// Vive en Infrastructure porque es un detalle de integración externa;
/// los casos de uso que lo necesiten lo inyectan por esta interfaz.
/// </summary>
public interface IServicioMensajeria
{
    Task<bool> EnviarAsync(MensajeSimple mensaje, CancellationToken cancellationToken = default);
}

/// <summary>Mensaje simple para enviar por el canal de mensajería configurado.</summary>
public sealed record MensajeSimple(string TelefonoDestino, string Texto);

/// <summary>
/// Implementación de PRUEBA de IServicioMensajeria: en lugar de enviar por WhatsApp,
/// registra el mensaje en el log. Permite desarrollar y probar toda la lógica sin
/// depender de credenciales externas. La implementación real se agrega después
/// implementando esta misma interfaz, sin tocar la lógica de negocio.
/// </summary>
public sealed class MensajeriaConsola : IServicioMensajeria
{
    private readonly ILogger<MensajeriaConsola> _logger;

    public MensajeriaConsola(ILogger<MensajeriaConsola> logger) => _logger = logger;

    public Task<bool> EnviarAsync(MensajeSimple mensaje, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📲 [SIMULADO] Mensaje a {Telefono}: {Texto}",
            mensaje.TelefonoDestino, mensaje.Texto);
        return Task.FromResult(true);
    }
}
