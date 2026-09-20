using System;

namespace BetterGIProWpf.Services;

public enum ErrorCode
{
    None = 0,
    ServiceUnavailable = 1001,
    Timeout = 1002,
    ModelInferenceFailed = 1003,
    GameWindowNotFound = 1004,
    InputInjectionFailed = 1005,
    SecurityStop = 1006,
    NetworkError = 1007,
    UserCancel = 1008,
    VersionMismatch = 1009,
    InternalError = 9999
}

public class ServiceResult
{
    public ErrorCode Code { get; init; } = ErrorCode.None;
    public string Message { get; init; } = "";
    public Exception? Exception { get; init; }
    public bool Ok => Code == ErrorCode.None;

    public static ServiceResult Success() => new() { Code = ErrorCode.None };
    public static ServiceResult Fail(ErrorCode code, string msg, Exception? ex = null) =>
        new() { Code = code, Message = msg, Exception = ex };

    public override string ToString() => Ok ? "OK" : $"[{Code}] {Message}";
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }
    public static ServiceResult<T> Success(T data) => new() { Code = ErrorCode.None, Data = data };
    public new static ServiceResult<T> Fail(ErrorCode code, string msg, Exception? ex = null) =>
        new() { Code = code, Message = msg, Exception = ex };
}
