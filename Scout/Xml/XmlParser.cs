using System.Xml.Linq;

namespace Scout.Xml;

public static class XmlParser
{
    /// <summary>
    /// Parses TB_SVC_SURVEYDESC from plaintext XML bytes and returns the first record's fields.
    /// Returns null if no record is found.
    /// </summary>
    public static SurveyFields? Parse(byte[] xmlBytes)
    {
        XDocument doc;
        try
        {
            using var ms = new MemoryStream(xmlBytes, writable: false);
            doc = XDocument.Load(ms);
        }
        catch (Exception)
        {
            return null;
        }

        var row = doc.Descendants("TB_SVC_SURVEYDESC").FirstOrDefault();
        if (row is null) return null;

        return new SurveyFields
        {
            SurveyJobNo  = row.Element("SURVEYJOB_NO")?.Value.NullIfEmpty(),
            OwnerName    = row.Element("OWNER_NAME")?.Value.NullIfEmpty(),
            QueueDate    = ParseDate(row.Element("QUEUE_DATE")?.Value),
            ProvinceName = row.Element("PROVINCE_NAME")?.Value.NullIfEmpty(),
            AmphurSeq    = ParseInt(row.Element("AMPHUR_SEQ")?.Value),
            TambolSeq    = ParseInt(row.Element("TAMBOL_SEQ")?.Value),
            LandNo       = ParseInt(row.Element("LAND_NO")?.Value),
            SurveyNo     = ParseInt(row.Element("SURVEY_NO")?.Value),
            SurveyorName = row.Element("SURVEYOR_NAME")?.Value.NullIfEmpty(),
        };
    }

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParse(s, out var d) ? d : null;
    }

    private static int? ParseInt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return int.TryParse(s, out var n) ? n : null;
    }
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string s)
        => string.IsNullOrWhiteSpace(s) ? null : s;
}
