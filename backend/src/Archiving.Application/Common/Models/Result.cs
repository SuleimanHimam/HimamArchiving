namespace Archiving.Application.Common.Models;

/// <summary>Lightweight success/failure result with an optional value and error message.</summary>
public sealed class Result<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }

    public static Result<T> Ok(T value) => new() { Succeeded = true, Value = value };
    public static Result<T> Fail(string error) => new() { Succeeded = false, Error = error };
}
