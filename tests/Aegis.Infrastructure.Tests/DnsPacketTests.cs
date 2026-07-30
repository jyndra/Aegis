using System.Text;
using Aegis.Infrastructure.Dns;
using FluentAssertions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class DnsPacketTests
{
    [Fact]
    public void Parse_ValidDnsQueryHeader_ParsesTransactionIdAndFlags()
    {
        byte[] rawHeader = new byte[]
        {
            0x12, 0x34, // Transaction ID: 0x1234
            0x01, 0x00, // Flags: Standard Query
            0x00, 0x01, // QDCount: 1
            0x00, 0x00, // ANCount: 0
            0x00, 0x00, // NSCount: 0
            0x00, 0x00, // ARCount: 0
            // Question: "example.com", Type A, Class IN
            0x07, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
            0x03, (byte)'c', (byte)'o', (byte)'m',
            0x00,       // End of string
            0x00, 0x01, // Type A
            0x00, 0x01  // Class IN
        };

        var packet = DnsPacket.Parse(rawHeader);

        packet.TransactionId.Should().Be(0x1234);
        packet.QuestionCount.Should().Be(1);
        packet.Questions.Should().HaveCount(1);

        var q = packet.Questions[0];
        q.Domain.Should().Be("example.com");
        q.Type.Should().Be(DnsQueryType.A);
    }

    [Fact]
    public void BuildBlockResponse_ModifiesHeaderAndAppendsNullIpAddress()
    {
        byte[] rawHeader = new byte[]
        {
            0xAB, 0xCD, // Transaction ID
            0x01, 0x00, // Standard query
            0x00, 0x01, // 1 Question
            0x00, 0x00,
            0x00, 0x00,
            0x00, 0x00,
            0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t',
            0x00,
            0x00, 0x01, // Type A
            0x00, 0x01  // Class IN
        };

        byte[] response = DnsPacket.BuildBlockResponse(rawHeader);

        response.Should().NotBeNull();
        response.Length.Should().BeGreaterThan(rawHeader.Length);

        // Check Response Flag set (0x8180)
        response[2].Should().Be(0x81);
        response[3].Should().Be(0x80);

        // Check ANCount = 1
        response[6].Should().Be(0x00);
        response[7].Should().Be(0x01);
    }

    [Fact]
    public void CompressionPointerLoop_ThrowsInvalidOperationException()
    {
        // Construct a circular compression pointer
        byte[] loopBuffer = new byte[]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xC0, 0x0E, // Pointer to offset 14
            0xC0, 0x0C  // Pointer back to offset 12 -> Loop!
        };

        int offset = 12;
        Action act = () => DnsPacket.ReadDomainName(loopBuffer, ref offset);

        act.Should().Throw<InvalidOperationException>().WithMessage("*compression pointer loop*");
    }
}
