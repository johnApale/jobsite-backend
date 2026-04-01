using Jobsite.SharedKernel.Errors;

namespace Jobsite.SharedKernel.Results;

/// <summary>
/// Lightweight result wrapper for operations that can fail without throwing.
/// Use when the caller needs to inspect the outcome; prefer <see cref="AppError"/> for flow-stopping errors.
/// </summary>
public sealed class Result<T>
{
    public T? Value { get; }
    public AppError? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    private Result(T value)
    {
        Value = value;
    }

    private Result(AppError error)
    {
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(AppError error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(AppError error) => Failure(error);
}
