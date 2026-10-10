namespace CvApi;

public sealed class CvEntity
{
    public long Id { get; set; }

    public string Owner { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public CvData Data { get; set; } = new();

    public CvResponse Response()
    {
        return new CvResponse(Id, CreatedAt, UpdatedAt, Data);
    }
}
