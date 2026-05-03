using System.Text;

namespace Backend.Services.Grading;

internal static class DockerBuildContextFactory
{
    public static Stream Create()
    {
        var stream = new MemoryStream();
        var dockerfileBytes = Encoding.UTF8.GetBytes(GraderDockerfile.Content);

        WriteHeader(stream, "Dockerfile", dockerfileBytes.Length);
        stream.Write(dockerfileBytes);
        WritePadding(stream, dockerfileBytes.Length);
        stream.Write(new byte[1024]);
        stream.Position = 0;

        return stream;
    }

    private static void WriteHeader(Stream stream, string fileName, int fileLength)
    {
        var header = new byte[512];
        WriteAscii(header, 0, 100, fileName);
        WriteOctal(header, 100, 8, 0x1A4);
        WriteOctal(header, 108, 8, 0);
        WriteOctal(header, 116, 8, 0);
        WriteOctal(header, 124, 12, fileLength);
        WriteOctal(header, 136, 12, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Fill(header, 148, 8, (byte)' ');
        header[156] = (byte)'0';
        WriteAscii(header, 257, 6, "ustar");
        WriteAscii(header, 263, 2, "00");

        var checksum = header.Sum(item => (int)item);
        WriteChecksum(header, checksum);
        stream.Write(header);
    }

    private static void WriteAscii(byte[] buffer, int offset, int length, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, length));
    }

    private static void WriteOctal(byte[] buffer, int offset, int length, long value)
    {
        var text = Convert.ToString(value, 8).PadLeft(length - 1, '0');
        WriteAscii(buffer, offset, length, text);
    }

    private static void WriteChecksum(byte[] buffer, int checksum)
    {
        var text = Convert.ToString(checksum, 8).PadLeft(6, '0');
        WriteAscii(buffer, 148, 6, text);
        buffer[154] = 0;
        buffer[155] = (byte)' ';
    }

    private static void Fill(byte[] buffer, int offset, int length, byte value)
    {
        Array.Fill(buffer, value, offset, length);
    }

    private static void WritePadding(Stream stream, int fileLength)
    {
        var padding = 512 - fileLength % 512;
        if (padding == 512)
        {
            return;
        }

        stream.Write(new byte[padding]);
    }
}
