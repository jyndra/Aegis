using System.Text;

namespace Aegis.Infrastructure.Dns;

public enum DnsQueryType : ushort
{
    A = 1,
    NS = 2,
    CNAME = 5,
    SOA = 6,
    PTR = 12,
    MX = 15,
    TXT = 16,
    AAAA = 28,
    ANY = 255
}

public class DnsQuestion
{
    public string Domain { get; set; } = string.Empty;
    public DnsQueryType Type { get; set; }
    public ushort Class { get; set; }
}

public class DnsPacket
{
    public ushort TransactionId { get; set; }
    public ushort Flags { get; set; }
    public bool IsResponse => (Flags & 0x8000) != 0;
    public ushort QuestionCount { get; set; }
    public ushort AnswerCount { get; set; }
    public ushort AuthorityCount { get; set; }
    public ushort AdditionalCount { get; set; }

    public List<DnsQuestion> Questions { get; set; } = new();

    public static DnsPacket Parse(byte[] buffer)
    {
        if (buffer == null || buffer.Length < 12)
        {
            throw new ArgumentException("Buffer too short for DNS header", nameof(buffer));
        }

        var packet = new DnsPacket
        {
            TransactionId = (ushort)((buffer[0] << 8) | buffer[1]),
            Flags = (ushort)((buffer[2] << 8) | buffer[3]),
            QuestionCount = (ushort)((buffer[4] << 8) | buffer[5]),
            AnswerCount = (ushort)((buffer[6] << 8) | buffer[7]),
            AuthorityCount = (ushort)((buffer[8] << 8) | buffer[9]),
            AdditionalCount = (ushort)((buffer[10] << 8) | buffer[11])
        };

        int offset = 12;
        for (int i = 0; i < packet.QuestionCount && offset < buffer.Length; i++)
        {
            string domain = ReadDomainName(buffer, ref offset);
            if (offset + 4 > buffer.Length)
            {
                break;
            }

            ushort type = (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
            ushort qclass = (ushort)((buffer[offset + 2] << 8) | buffer[offset + 3]);
            offset += 4;

            packet.Questions.Add(new DnsQuestion
            {
                Domain = domain,
                Type = (DnsQueryType)type,
                Class = qclass
            });
        }

        return packet;
    }

    public static string ReadDomainName(byte[] buffer, ref int offset)
    {
        var domainBuilder = new StringBuilder();
        int currentOffset = offset;
        bool jumped = false;
        int originalOffsetAfterJump = -1;
        int jumpCount = 0;
        const int maxJumps = 10;

        while (currentOffset < buffer.Length)
        {
            byte length = buffer[currentOffset];

            // End of domain name
            if (length == 0)
            {
                currentOffset++;
                if (!jumped)
                {
                    offset = currentOffset;
                }
                break;
            }

            // Compression pointer check (top 2 bits set: 0xC0)
            if ((length & 0xC0) == 0xC0)
            {
                if (currentOffset + 1 >= buffer.Length)
                {
                    break;
                }

                if (!jumped)
                {
                    originalOffsetAfterJump = currentOffset + 2;
                    jumped = true;
                }

                jumpCount++;
                if (jumpCount > maxJumps)
                {
                    throw new InvalidOperationException("DNS compression pointer loop detected.");
                }

                int pointerOffset = ((length & 0x3F) << 8) | buffer[currentOffset + 1];
                currentOffset = pointerOffset;
                continue;
            }

            currentOffset++;
            if (currentOffset + length > buffer.Length)
            {
                break;
            }

            if (domainBuilder.Length > 0)
            {
                domainBuilder.Append('.');
            }

            domainBuilder.Append(Encoding.ASCII.GetString(buffer, currentOffset, length));
            currentOffset += length;
        }

        if (jumped)
        {
            offset = originalOffsetAfterJump;
        }

        return domainBuilder.ToString().ToLowerInvariant();
    }

    public static byte[] BuildBlockResponse(byte[] rawQueryPacket)
    {
        if (rawQueryPacket == null || rawQueryPacket.Length < 12)
        {
            return Array.Empty<byte>();
        }

        byte[] response = (byte[])rawQueryPacket.Clone();

        // Standard Response Flags: QR=1 (Response), AA=1, RA=1, RCODE=0 (NoError)
        // Set Header Flags: 0x8180 (Standard query response, No error)
        response[2] = 0x81;
        response[3] = 0x80;

        // Set ANCount (Answer Count) = 1
        response[6] = 0x00;
        response[7] = 0x01;

        // Append A record answer pointing to 0.0.0.0
        var answerStream = new MemoryStream();
        answerStream.Write(response, 0, response.Length);

        // Name pointer pointing to start of Question section (0xC00C)
        answerStream.WriteByte(0xC0);
        answerStream.WriteByte(0x0C);

        // Type: A (0x0001)
        answerStream.WriteByte(0x00);
        answerStream.WriteByte(0x01);

        // Class: IN (0x0001)
        answerStream.WriteByte(0x00);
        answerStream.WriteByte(0x01);

        // TTL: 60 seconds (0x0000003C)
        answerStream.WriteByte(0x00);
        answerStream.WriteByte(0x00);
        answerStream.WriteByte(0x00);
        answerStream.WriteByte(0x3C);

        // Data Length: 4 bytes
        answerStream.WriteByte(0x00);
        answerStream.WriteByte(0x04);

        // IP Address: 0.0.0.0
        answerStream.WriteByte(0x00);
        answerStream.WriteByte(0x00);
        answerStream.WriteByte(0x00);
        answerStream.WriteByte(0x00);

        return answerStream.ToArray();
    }

    public static byte[] BuildNxDomainResponse(byte[] rawQueryPacket)
    {
        if (rawQueryPacket == null || rawQueryPacket.Length < 12)
        {
            return Array.Empty<byte>();
        }

        byte[] response = (byte[])rawQueryPacket.Clone();

        // Response Flags: QR=1, RA=1, RCODE=3 (NXDOMAIN) -> 0x8183
        response[2] = 0x81;
        response[3] = 0x83;

        return response;
    }
}
