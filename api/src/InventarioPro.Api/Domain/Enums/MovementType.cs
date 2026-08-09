namespace InventarioPro.Api.Domain.Enums;

/// <summary>Tipo de movimiento de stock.</summary>
public enum MovementType
{
    /// <summary>Ingreso de mercadería (compra, devolución de cliente).</summary>
    In = 1,

    /// <summary>Egreso (venta, rotura, devolución a proveedor).</summary>
    Out = 2,

    /// <summary>Ajuste por conteo físico: fija el stock a un valor absoluto.</summary>
    Adjustment = 3
}
