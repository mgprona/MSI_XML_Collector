using System.Security.Cryptography;
using System.Xml.Linq;

namespace Scout.Crypto;

/// <summary>
/// Decrypts TB_ENCRYPT XML in-memory. No decrypted bytes are written to disk.
/// Key file format: raw binary AES key (16, 24, or 32 bytes = AES-128/192/256).
/// Ciphertext format: first 16 bytes = IV, remainder = AES-CBC ciphertext.
/// </summary>
public class AesDecryptor
{
    private readonly byte[] _key;

    private AesDecryptor(byte[] key) => _key = key;

    public static AesDecryptor LoadFromFile(string keyFilePath)
    {
        if (!File.Exists(keyFilePath))
            throw new FileNotFoundException($"secrets.key not found: {keyFilePath}");

        var key = File.ReadAllBytes(keyFilePath);

        if (key.Length is not (16 or 24 or 32))
            throw new InvalidOperationException(
                $"secrets.key must be 16, 24, or 32 bytes (AES-128/192/256). Got {key.Length} bytes.");

        return new AesDecryptor(key);
    }

    /// <summary>
    /// Takes the full encrypted XML document bytes, extracts the Base64 ciphertext
    /// from TB_ENCRYPT/data, decrypts it, and returns the plaintext XML bytes.
    /// </summary>
    public byte[] DecryptXml(byte[] encryptedXmlBytes)
    {
        XDocument doc;
        using (var ms = new MemoryStream(encryptedXmlBytes, writable: false))
            doc = XDocument.Load(ms);

        var dataElement = doc.Descendants("TB_ENCRYPT")
                             .Select(e => e.Element("data"))
                             .FirstOrDefault(e => e is not null)
            ?? throw new InvalidDataException("No TB_ENCRYPT/data element found.");

        var ciphertext = Convert.FromBase64String(dataElement.Value.Trim());
        return DecryptAesCbc(ciphertext);
    }

    private byte[] DecryptAesCbc(byte[] ciphertext)
    {
        if (ciphertext.Length < 16)
            throw new InvalidDataException("Ciphertext too short to contain IV.");

        var iv         = ciphertext[..16];
        var payload    = ciphertext[16..];

        using var aes = Aes.Create();
        aes.Key     = _key;
        aes.IV      = iv;
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        using var ms        = new MemoryStream(payload, writable: false);
        using var cs        = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result    = new MemoryStream();

        cs.CopyTo(result);
        return result.ToArray();
    }
}
