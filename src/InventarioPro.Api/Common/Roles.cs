namespace InventarioPro.Api.Common;

/// <summary>Roles de la aplicación. Constantes para evitar strings mágicos en los atributos.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Manager, Viewer];

    /// <summary>Puede escribir: crear, editar y mover stock.</summary>
    public const string CanWrite = $"{Admin},{Manager}";
}
