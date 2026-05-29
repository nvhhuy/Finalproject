namespace Shared.Common;

/// <summary>
///     Discriminated success/failure for application flows without exceptions.
/// </summary>
public readonly struct Result<TSuccess, TError>
{
    private readonly TSuccess? _success;
    private readonly TError? _error;

    private Result(TSuccess success)
    {
        IsSuccess = true;
        _success = success;
        _error = default;
    }

    private Result(TError error)
    {
        IsSuccess = false;
        _success = default;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public TSuccess Value =>
        IsSuccess ? _success! : throw new InvalidOperationException("Result has no success value.");

    public TError Error =>
        !IsSuccess ? _error! : throw new InvalidOperationException("Result has no error value.");

    public static implicit operator Result<TSuccess, TError>(TSuccess success)
    {
        return new Result<TSuccess, TError>(success);
    }

    public static implicit operator Result<TSuccess, TError>(TError error)
    {
        return new Result<TSuccess, TError>(error);
    }
}