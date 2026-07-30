using Aegis.Core.Errors;
using FluentAssertions;
using Xunit;

namespace Aegis.Core.Tests;

public class AegisExceptionTests
{
    [Fact]
    public void AegisException_SetsErrorCodeAndMessage()
    {
        var ex = new AegisException(AegisErrorCodes.LockActive, "Lock is active");

        ex.ErrorCode.Should().Be("AEGIS-3001");
        ex.Message.Should().Be("Lock is active");
    }

    [Fact]
    public void AegisException_PreservesInnerException()
    {
        var inner = new InvalidOperationException("Inner failure");
        var ex = new AegisException(AegisErrorCodes.DatabaseCorrupted, "Corrupted DB", inner);

        ex.ErrorCode.Should().Be("AEGIS-8001");
        ex.Message.Should().Be("Corrupted DB");
        ex.InnerException.Should().BeSameAs(inner);
    }
}
