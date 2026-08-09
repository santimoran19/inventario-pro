namespace InventarioPro.Api.Common;

/// <summary>Página de resultados con la metadata necesaria para paginar en el cliente.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>Parámetros de paginación con topes duros para que nadie pida 1.000.000 de filas.</summary>
public class PageQuery
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;
    private int _page = 1;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 1,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }
}

/// <summary>Error de negocio esperado. Se traduce a 400/404/409 en el middleware.</summary>
public class DomainException(string message, int statusCode = StatusCodes.Status400BadRequest)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class NotFoundException(string resource, object key)
    : DomainException($"{resource} con identificador '{key}' no fue encontrado.", StatusCodes.Status404NotFound);

public class ConflictException(string message)
    : DomainException(message, StatusCodes.Status409Conflict);
