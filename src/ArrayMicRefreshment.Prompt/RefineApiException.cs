namespace ArrayMicRefreshment.Prompt;

public sealed class RefineApiException : Exception
{
    public RefineApiException(string message)
        : base(message)
    {
    }

    public RefineApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
