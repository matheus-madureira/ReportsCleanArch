using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using Reports.Application.Abstractions.Data;
using Reports.Application.Abstractions.Documents;
using Reports.Infrastructure.Documents;
using Reports.Infrastructure.Persistence;
using Reports.Infrastructure.Persistence.Repositories;

namespace Reports.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddSingleton<IPdfGenerator, QuestPdfGenerator>();

        return services;
    }
}
