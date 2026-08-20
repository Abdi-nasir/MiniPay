namespace MiniApy.Api.Exceptions;

public sealed class ResourceConflictException : Exception
{
    public ResourceConflictException(string message)
        : base(message)
    {
    }
}