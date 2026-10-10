namespace CvApi;

public sealed class ApiException : Exception
{
    public ApiException(int status, string message) : base(message)
    {
        Status = status;
    }

    public int Status { get; }

}
