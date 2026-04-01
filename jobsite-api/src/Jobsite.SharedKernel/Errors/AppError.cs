namespace Jobsite.SharedKernel.Errors;

/// <summary>
/// Typed exception for domain and application errors.
/// Caught by <c>AppErrorMiddleware</c> and serialized into the canonical error envelope.
/// </summary>
public sealed class AppError : Exception
{
    /// <summary>Machine-readable error code in <c>SCREAMING_SNAKE_CASE</c>.</summary>
    public string Code { get; }

    /// <summary>HTTP status code to return.</summary>
    public int StatusCode { get; }

    /// <summary>Per-field validation details. Omitted from response when null.</summary>
    public Dictionary<string, string>? Details { get; private set; }

    public AppError(string code, int statusCode, string message)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    /// <summary>Returns a new <see cref="AppError"/> with a custom message, preserving code and status.</summary>
    public AppError WithMessage(string message)
    {
        return new AppError(Code, StatusCode, message) { Details = Details };
    }

    /// <summary>Returns a new <see cref="AppError"/> with validation details attached.</summary>
    public AppError WithDetails(Dictionary<string, string> details)
    {
        return new AppError(Code, StatusCode, Message) { Details = details };
    }
}
