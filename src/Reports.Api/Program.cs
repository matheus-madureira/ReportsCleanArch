using Reports.Api.Endpoints;
using Reports.Application.Invoices.GetInvoicePdf;
using Reports.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure();
builder.Services.AddScoped<GetInvoicePdfHandler>();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.MapInvoiceEndpoints();

app.Run();

public partial class Program;
