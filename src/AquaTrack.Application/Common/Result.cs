namespace AquaTrack.Application.Common;

/// <summary>
/// Outcome of a service call. Error texts are English keys, translated in the Web layer
/// through the shared Urdu/English resources.
/// </summary>
public class Result
{
    protected Result(bool succeeded, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public bool Succeeded { get; }
    public IReadOnlyList<string> Errors { get; }

    public static Result Ok() => new(true, Array.Empty<string>());
    public static Result Fail(params string[] errors) => new(false, errors);
}

public class Result<T> : Result
{
    private Result(bool succeeded, T? value, IReadOnlyList<string> errors) : base(succeeded, errors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Ok(T value) => new(true, value, Array.Empty<string>());
    public static new Result<T> Fail(params string[] errors) => new(false, default, errors);
}
