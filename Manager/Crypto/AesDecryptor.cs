using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace Manager.Crypto;

public sealed class AesDecryptor
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    private AesDecryptor(byte[] key, byte[] iv)
    {
        _key = key;
        _iv = iv;
    }

    public static AesDecryptor LoadFromFile(string keyFilePath)
    {
        var data = File.ReadAllBytes(keyFilePath);

        if (data.Length != 48)
            throw new InvalidOperationException(
                $"secrets.key ต้องมีขนาด 48 bytes (32 key + 16 IV) แต่ไฟล์นี้มี {data.Length} bytes");

        return new AesDecryptor(data[..32], data[32..]);
    }

    public byte[] DecryptXml(byte[] encryptedXmlBytes)
    {
        XDocument doc;
        using (var ms = new MemoryStream(encryptedXmlBytes, writable: false))
            doc = XDocument.Load(ms);

        var dataElement = doc.Descendants("TB_ENCRYPT")
                             .Select(e => e.Element("data"))
                             .FirstOrDefault(e => e is not null)
            ?? throw new InvalidDataException("ไม่พบ TB_ENCRYPT/data ใน XML");

        var ciphertext = Convert.FromBase64String(dataElement.Value.Trim());
        return DecryptAesCbc(ciphertext);
    }

    private byte[] DecryptAesCbc(byte[] ciphertext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(ciphertext, writable: false);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result = new MemoryStream();

        cs.CopyTo(result);
        return result.ToArray();
    }
}
