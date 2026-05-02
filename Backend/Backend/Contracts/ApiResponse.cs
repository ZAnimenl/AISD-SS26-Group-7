namespace Backend.Contracts;

public sealed record ApiResponse<T>(bool Ok, T? Data, ApiError? Error)
{
    public static ApiResponse<T> Success(T data)
    {
        return new ApiResponse<T>(true, data, null);
    }

    public static ApiResponse<T> Failure(string code, string message)
    {
        return new ApiResponse<T>(false, default, new ApiError(code, message));
    }
}
