using System.Security.Cryptography;
using System.Text;

namespace MedAssist.Shared.Models;

/// <summary>
/// Produces a stable GUID from a string key, so identical inputs always map to the same id.
/// Used for Qdrant point ids derived from a chunk's identity ("{bookId}:{chunkIndex}", or
/// "summary:{bookId}:{chunkIndex}") so re-indexing overwrites rather than duplicates (audit P1-6).
/// Not a random id: same key → same GUID, across processes and runs.
/// </summary>
public static class DeterministicGuid
{
    public static Guid Create(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("medassist:" + key));

        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);

        // Stamp RFC 4122 version (8 = custom) and variant bits so it's a well-formed UUID.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
