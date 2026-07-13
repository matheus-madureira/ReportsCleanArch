using Reports.Application.Common;
using Reports.Application.Invoices.GetInvoicePdf;

namespace Reports.Api.Endpoints;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/invoices/{id:guid}/pdf", async (
            Guid id,
            GetInvoicePdfHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetInvoicePdfQuery(id), ct);

            return result.IsSuccess
                ? Results.File(result.Value, "application/pdf", $"invoice-{id}.pdf")
                : result.ToProblem();
        })
        .WithName("GetInvoicePdf")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// Converte um <see cref="Result"/> de falha em <see cref="ProblemDetails"/>, mapeando
    /// o código do erro para o status HTTP: <c>*.Invalid*</c> → 400; <c>*.NotFound</c>/<c>*.Empty</c> → 404.
    /// </summary>
    private static IResult ToProblem(this Result result)
    {
        var error = result.Error;

        var statusCode = error.Code switch
        {
            var code when code.Contains("Invalid", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status400BadRequest,
            var code when code.EndsWith("NotFound", StringComparison.OrdinalIgnoreCase)
                         || code.EndsWith("Empty", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(
            title: error.Message,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
