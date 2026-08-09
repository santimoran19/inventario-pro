using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioPro.Api.Common;

/// <summary>
/// Convierte cualquier excepción no manejada en una respuesta ProblemDetails (RFC 7807).
///
/// Seguridad: las excepciones inesperadas nunca exponen el mensaje real ni el stack trace
/// al cliente — se loguean completas del lado del servidor y afuera sale un mensaje genérico.
/// Filtrar un stack trace revela rutas, versiones de paquetes y estructura interna.
/// </summary>
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        var (status, title, detail) = exception switch
        {
            DomainException dex =>
                (dex.StatusCode, "Solicitud inválida", dex.Message),

            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict,
                 "Conflicto de concurrencia",
                 "El registro fue modificado por otra operación. Volvé a cargar los datos e intentá de nuevo."),

            // 499 no existe en StatusCodes: es una convención de nginx para "cliente cortó la conexión".
            OperationCanceledException =>
                (499, "Solicitud cancelada", "La solicitud fue cancelada."),

            _ => (StatusCodes.Status500InternalServerError,
                  "Error interno",
                  "Ocurrió un error inesperado procesando la solicitud.")
        };

        if (status >= 500)
        {
            logger.LogError(exception,
                "Excepción no manejada. TraceId={TraceId} Path={Path}",
                traceId, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(
                "Solicitud rechazada ({Status}). TraceId={TraceId} Path={Path} Motivo={Reason}",
                status, traceId, httpContext.Request.Path, exception.Message);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = traceId;

        // El detalle técnico solo se agrega en desarrollo.
        if (env.IsDevelopment() && status >= 500)
        {
            problem.Extensions["exception"] = exception.ToString();
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
