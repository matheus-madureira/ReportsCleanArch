using Reports.Domain.Common;

namespace Reports.Domain.UnitTests;

public class ErrorTests
{
    [Fact]
    public void Errors_WithSameCodeAndMessage_AreEqual()
    {
        var a = new Error("Invoice.NotFound", "Fatura não encontrada.");
        var b = new Error("Invoice.NotFound", "Fatura não encontrada.");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Errors_WithDifferentCode_AreNotEqual()
    {
        var a = new Error("Invoice.NotFound", "Fatura não encontrada.");
        var b = new Error("Invoice.Invalid", "Fatura não encontrada.");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void None_HasEmptyCodeAndMessage()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Message);
    }
}
