namespace Paghe.Persistence.Entities;

/// <summary>
/// Fotografia immutabile dei dati controllati. Il PDF chiuso viene creato esclusivamente da
/// questa fotografia e conservato in un record separato, anch'esso append-only.
/// </summary>
public sealed class CedolinoSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CedolinoId { get; set; }
    public int VersioneSchema { get; set; } = 1;
    [System.Text.Json.Serialization.JsonIgnore]
    public string DatiProtetti { get; set; } = "";
    public string HashDatiSha256 { get; set; } = "";
    public DateTime CreatoIl { get; set; }
    public Guid CreatoDa { get; set; }
}

/// <summary>PDF finale, creato una sola volta dallo snapshot al momento della chiusura.</summary>
public sealed class CedolinoDocumentoEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CedolinoId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string PdfProtetto { get; set; } = "";
    public string HashPdfSha256 { get; set; } = "";
    public string HashFirmaSiglaTimbroSha256 { get; set; } = "";
    public string VersioneRenderer { get; set; } = "cedolino-v1:non-specificato";
    public DateTime CreatoIl { get; set; }
    public Guid CreatoDa { get; set; }
}
