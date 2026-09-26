using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NoMoreLunarPlus.Asar;

internal sealed class AsarArchive : IDisposable
{
    private const int IntegrityBlockSize = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions HeaderJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly FileStream _stream;
    private readonly JsonObject _header;
    private readonly long _dataOffset;

    private AsarArchive(FileStream stream, JsonObject header, byte[] headerBytes, long dataOffset)
    {
        _stream = stream;
        _header = header;
        _dataOffset = dataOffset;
        HeaderHash = Sha256Hex(headerBytes);
    }

    public string HeaderHash { get; }

    public IEnumerable<AsarEntry> Files => Walk(_header, string.Empty);

    public static AsarArchive Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        try
        {
            Span<byte> prefix = stackalloc byte[16];
            stream.ReadExactly(prefix);

            if (BinaryPrimitives.ReadUInt32LittleEndian(prefix) != 4)
            {
                throw new InvalidDataException($"'{path}' is not a valid asar archive.");
            }

            var headerPickleSize = BinaryPrimitives.ReadUInt32LittleEndian(prefix[4..]);
            var headerLength = BinaryPrimitives.ReadInt32LittleEndian(prefix[12..]);
            var headerBytes = new byte[headerLength];
            stream.ReadExactly(headerBytes);

            var header = JsonNode.Parse(headerBytes) as JsonObject
                ?? throw new InvalidDataException($"'{path}' has an unreadable header.");

            return new AsarArchive(stream, header, headerBytes, 8 + headerPickleSize);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public bool Contains(string path) => Find(_header, path) is not null;

    public byte[] ReadFile(string path)
    {
        var node = Find(_header, path) ?? throw new FileNotFoundException($"'{path}' does not exist in the archive.");
        var entry = new AsarEntry(path, node);

        if (!entry.IsPacked)
        {
            throw new InvalidOperationException($"'{path}' is not packed inside the archive.");
        }

        var buffer = new byte[entry.Size];
        _stream.Position = _dataOffset + entry.Offset;
        _stream.ReadExactly(buffer);
        return buffer;
    }

    public string Save(string destination, IReadOnlyDictionary<string, byte[]> replacements)
    {
        var header = _header.DeepClone().AsObject();

        foreach (var path in replacements.Keys)
        {
            EnsureFile(header, path);
        }

        var pending = Walk(header, string.Empty)
            .Where(entry => entry.IsPacked || replacements.ContainsKey(entry.Path))
            .Select(entry => new PendingEntry(
                entry.Node,
                entry.IsPacked ? entry.Offset : long.MaxValue,
                entry.IsPacked ? entry.Size : 0,
                replacements.GetValueOrDefault(entry.Path)))
            .OrderBy(entry => entry.SourceOffset)
            .ToList();

        long cursor = 0;

        foreach (var entry in pending)
        {
            entry.Node["offset"] = cursor.ToString(CultureInfo.InvariantCulture);

            if (entry.Content is not null)
            {
                entry.Node["size"] = entry.Content.LongLength;
                entry.Node["integrity"] = BuildIntegrity(entry.Content);
            }

            cursor += entry.Content?.LongLength ?? entry.SourceSize;
        }

        var headerBytes = Encoding.UTF8.GetBytes(header.ToJsonString(HeaderJsonOptions));

        using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            WriteHeader(output, headerBytes);

            foreach (var entry in pending)
            {
                if (entry.Content is not null)
                {
                    output.Write(entry.Content);
                }
                else
                {
                    CopyRange(output, _dataOffset + entry.SourceOffset, entry.SourceSize);
                }
            }
        }

        return Sha256Hex(headerBytes);
    }

    public void Dispose() => _stream.Dispose();

    private void CopyRange(Stream output, long start, long length)
    {
        var buffer = new byte[81920];
        _stream.Position = start;

        while (length > 0)
        {
            var read = _stream.Read(buffer, 0, (int)Math.Min(buffer.Length, length));

            if (read == 0)
            {
                throw new EndOfStreamException("The archive ended before all file data was read.");
            }

            output.Write(buffer, 0, read);
            length -= read;
        }
    }

    private static void WriteHeader(Stream output, byte[] headerBytes)
    {
        var alignedLength = (headerBytes.Length + 3) & ~3;
        var headerPickleSize = (uint)(8 + alignedLength);

        Span<byte> prefix = stackalloc byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(prefix[4..], headerPickleSize);
        BinaryPrimitives.WriteUInt32LittleEndian(prefix[8..], headerPickleSize - 4);
        BinaryPrimitives.WriteUInt32LittleEndian(prefix[12..], (uint)headerBytes.Length);

        output.Write(prefix);
        output.Write(headerBytes);
        output.Write(new byte[alignedLength - headerBytes.Length]);
    }

    private static JsonObject BuildIntegrity(byte[] content)
    {
        var blocks = new JsonArray();

        for (var offset = 0; offset < content.Length; offset += IntegrityBlockSize)
        {
            var length = Math.Min(IntegrityBlockSize, content.Length - offset);
            blocks.Add((JsonNode)Sha256Hex(content.AsSpan(offset, length)));
        }

        if (content.Length == 0)
        {
            blocks.Add((JsonNode)Sha256Hex([]));
        }

        return new JsonObject
        {
            ["algorithm"] = "SHA256",
            ["hash"] = Sha256Hex(content),
            ["blockSize"] = IntegrityBlockSize,
            ["blocks"] = blocks
        };
    }

    private static IEnumerable<AsarEntry> Walk(JsonObject directory, string prefix)
    {
        if (directory["files"] is not JsonObject children)
        {
            yield break;
        }

        foreach (var (name, child) in children)
        {
            if (child is not JsonObject node)
            {
                continue;
            }

            var path = prefix.Length == 0 ? name : $"{prefix}/{name}";

            if (node.ContainsKey("files"))
            {
                foreach (var nested in Walk(node, path))
                {
                    yield return nested;
                }
            }
            else
            {
                yield return new AsarEntry(path, node);
            }
        }
    }

    private static JsonObject? Find(JsonObject root, string path)
    {
        JsonObject? current = root;

        foreach (var segment in path.Split('/'))
        {
            current = (current?["files"] as JsonObject)?[segment] as JsonObject;
        }

        return current;
    }

    private static void EnsureFile(JsonObject root, string path)
    {
        var segments = path.Split('/');
        var current = root;

        for (var i = 0; i < segments.Length; i++)
        {
            if (current["files"] is not JsonObject children)
            {
                children = new JsonObject();
                current["files"] = children;
            }

            var isLast = i == segments.Length - 1;

            if (children[segments[i]] is not JsonObject next)
            {
                next = isLast ? new JsonObject { ["size"] = 0 } : new JsonObject { ["files"] = new JsonObject() };
                children[segments[i]] = next;
            }

            current = next;
        }
    }

    private static string Sha256Hex(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private sealed record PendingEntry(JsonObject Node, long SourceOffset, long SourceSize, byte[]? Content);
}
