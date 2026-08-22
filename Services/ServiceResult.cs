namespace AgendaApp.Services
{
    public enum ServiceErrorType
    {
        None,
        NotFound,
        Conflict,
        BadRequest,
        Unauthorized
    }

    public class ServiceResult<T>
    {
        public bool Success => ErrorType == ServiceErrorType.None;
        public T? Data { get; init; }
        public string? Error { get; init; }
        public ServiceErrorType ErrorType { get; init; } = ServiceErrorType.None;

        public static ServiceResult<T> Ok(T data) => new() { Data = data, ErrorType = ServiceErrorType.None };
        public static ServiceResult<T> NotFound(string message) => new() { Error = message, ErrorType = ServiceErrorType.NotFound };
        public static ServiceResult<T> Conflict(string message) => new() { Error = message, ErrorType = ServiceErrorType.Conflict };
        public static ServiceResult<T> BadRequest(string message) => new() { Error = message, ErrorType = ServiceErrorType.BadRequest };
        public static ServiceResult<T> Unauthorized(string message) => new() { Error = message, ErrorType = ServiceErrorType.Unauthorized };
    }

    public static class ServiceResult
    {
        public static ServiceResult<bool> Ok() => ServiceResult<bool>.Ok(true);
        public static ServiceResult<bool> NotFound(string message) => ServiceResult<bool>.NotFound(message);
        public static ServiceResult<bool> Conflict(string message) => ServiceResult<bool>.Conflict(message);
        public static ServiceResult<bool> BadRequest(string message) => ServiceResult<bool>.BadRequest(message);
        public static ServiceResult<bool> Unauthorized(string message) => ServiceResult<bool>.Unauthorized(message);
    }
}
