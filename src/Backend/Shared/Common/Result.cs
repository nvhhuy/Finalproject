namespace Shared.Common;

/// <summary>
/// Discriminated success/failure for application flows without exceptions.
/// </summary>
public readonly struct Result<TSuccess, TError>
{
    private readonly bool _isSuccess;
    private readonly TSuccess? _success;
    private readonly TError? _error;

    private Result(TSuccess success)
    {
        _isSuccess = true;
        _success = success;
        _error = default;
    }

    private Result(TError error)
    {
        _isSuccess = false;
        _success = default;
        _error = error;
    }

    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;

    public TSuccess Value =>
        _isSuccess ? _success! : throw new InvalidOperationException("Result has no success value.");

    public TError Error =>
        !_isSuccess ? _error! : throw new InvalidOperationException("Result has no error value.");

    public static implicit operator Result<TSuccess, TError>(TSuccess success) => new(success);
    public static implicit operator Result<TSuccess, TError>(TError error) => new(error);
}
