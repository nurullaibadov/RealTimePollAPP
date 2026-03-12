namespace RealTimePoll.API.Controllers;

public class ApiResponse<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? Message { get; private set; }
    public IEnumerable<string>? Errors { get; private set; }
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    private ApiResponse() { }

    public static ApiResponse<T> Success(T? data, string? message = null)
        => new ApiResponse<T> { IsSuccess = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(IEnumerable<string> errors, string? message = null)
        => new ApiResponse<T> { IsSuccess = false, Errors = errors, Message = message };
}
