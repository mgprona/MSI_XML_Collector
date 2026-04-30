namespace Scout.Xml;

public enum XmlFormat
{
    Unknown,
    Plaintext,   // root=<NewDataSet>, table=<TB_SVC_SURVEYDESC>
    Encrypted,   // root=<Root>, table=<TB_ENCRYPT>, data=Base64+AES
}
