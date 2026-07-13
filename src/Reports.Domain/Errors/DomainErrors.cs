using Reports.Domain.Common;

namespace Reports.Domain.Errors;

public static class DomainErrors
{
    public static class Invoice
    {
        public static readonly Error NotFound =
            new("Invoice.NotFound", "Fatura não encontrada.");
    }
}
