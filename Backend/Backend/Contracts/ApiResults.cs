using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Contracts;

public static class ApiResults
{
    public static Ok<ApiResponse<T>> Success<T>(T data)
    {
        return TypedResults.Ok(ApiResponse<T>.Success(data));
    }

    public static JsonHttpResult<ApiResponse<object>> Error(string code, string message, int statusCode)
    {
        return TypedResults.Json(ApiResponse<object>.Failure(code, message), statusCode: statusCode);
    }
}
