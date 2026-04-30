using System.Xml;

namespace Scout.Xml;

public static class XmlIdentifier
{
    /// <summary>
    /// Detects format from the document root element name only (fast, single-pass).
    ///   &lt;Root&gt;       → Encrypted  (TB_ENCRYPT inside)
    ///   &lt;NewDataSet&gt; → Plaintext  (TB_SVC_SURVEYDESC inside)
    /// </summary>
    public static XmlFormat Detect(byte[] bytes)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments   = true,
                DtdProcessing    = DtdProcessing.Ignore,
            };

            using var ms     = new MemoryStream(bytes, writable: false);
            using var reader = XmlReader.Create(ms, settings);

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;

                return reader.LocalName switch
                {
                    "Root"       => XmlFormat.Encrypted,
                    "NewDataSet" => XmlFormat.Plaintext,
                    _            => XmlFormat.Unknown,
                };
            }
        }
        catch (XmlException) { }

        return XmlFormat.Unknown;
    }
}
