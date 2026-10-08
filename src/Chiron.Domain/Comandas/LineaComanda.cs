namespace Chiron.Domain.Comandas;

/// <summary>
/// Línea de una comanda: un producto solicitado, su cantidad y el precio unitario al
/// momento de levantar la comanda. Se guarda el precio del momento (no se referencia el
/// precio actual del producto) para que el histórico sea fiel aunque luego cambie la carta.
///
/// Puede llevar una Nota del mesero para cocina (ej. "sin azúcar", "leche de almendra").
/// Setters privados para permitir el mapeo de EF Core manteniendo la inmutabilidad externa.
/// </summary>
public sealed class LineaComanda
{
    /// <summary>Producto solicitado.</summary>
    public Guid ProductoId { get; private set; }

    /// <summary>Nombre del producto al momento de la comanda (para la pantalla de cocina/ticket).</summary>
    public string NombreProducto { get; private set; }

    /// <summary>Cantidad solicitada.</summary>
    public int Cantidad { get; private set; }

    /// <summary>Precio unitario al momento de la comanda.</summary>
    public decimal PrecioUnitario { get; private set; }

    /// <summary>Nota opcional del mesero para cocina (preferencias/indicaciones).</summary>
    public string? Nota { get; private set; }

    /// <summary>Importe de la línea (cantidad × precio unitario).</summary>
    public decimal Subtotal => Cantidad * PrecioUnitario;

    public LineaComanda(Guid productoId, string nombreProducto, int cantidad, decimal precioUnitario, string? nota = null)
    {
        ProductoId = productoId;
        NombreProducto = nombreProducto;
        Cantidad = cantidad;
        PrecioUnitario = precioUnitario;
        Nota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
    }

    // Constructor privado sin parámetros para EF Core.
    private LineaComanda()
    {
        NombreProducto = string.Empty;
    }
}
