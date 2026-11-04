namespace SettleMate.Web.Common
{
    // An uploaded file saved to disk, before its row is written to dbo.Files.
    public record StoredFile(string Name, string OriginalName, string ContentType, bool Private, string Purpose);
}
