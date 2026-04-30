using System.Security.Cryptography;
using System.Xml.Linq;

namespace Scout.Crypto;

/// <summary>
/// Decrypts TB_ENCRYPT XML produced by the original MSI system (com.dol.samart).
///
/// secrets.key format: 48 raw bytes
///   [0..31] = AES-256 key  (SHA-256 of passphrase)
///   [32..47] = AES IV      (MD5 of passphrase)
///
/// Ciphertext in <data> = Base64(AES-CBC ciphertext) — IV is fixed, NOT prepended.
/// Decrypted payload = UTF-8 plaintext XML with <NewDataSet> structure.
/// Nothing is written to disk.
/// </summary>
public class AesDecryptor
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    private AesDecryptor(byte[] key, byte[] iv)
    {
        _key = key;
        _iv  = iv;
    }

    public static AesDecryptor LoadFromFile(string keyFilePath)
    {
        if (!File.Exists(keyFilePath))
            throw new FileNotFoundException($"secrets.key not found: {keyFilePath}");

        var data = File.ReadAllBytes(keyFilePath);

        if (data.Length != 48)
            throw new InvalidOperationException(
                $"secrets.key must be exactly 48 bytes (32 key + 16 IV). Got {data.Length} bytes.");

        return new AesDecryptor(key: data[..32], iv: data[32..]);
    }

    /// <summary>
    /// Extracts Base64 ciphertext from TB_ENCRYPT/data, decrypts, returns UTF-8 plaintext bytes.
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
        var plaintext  = DecryptAesCbc(ciphertext);
        return plaintext;
    }

    private byte[] DecryptAesCbc(byte[] ciphertext)
    {
        using var aes    = Aes.Create();
        aes.Key          = _key;
        aes.IV           = _iv;
        aes.Mode         = CipherMode.CBC;
        aes.Padding      = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        using var ms        = new MemoryStream(ciphertext, writable: false);
        using var cs        = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result    = new MemoryStream();

        cs.CopyTo(result);
        return result.ToArray();
    }
}
