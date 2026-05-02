using Backend.Contracts;

namespace OjSharp.Tests.ApiContractTests;

public sealed class ApiResponseTests
{
    [Fact]
    public void Success_contains_data_and_no_error()
    {
        var response = ApiResponse<object>.Success(new { value = 1 });

        Assert.True(response.Ok);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
    }

    [Fact]
    public void Failure_contains_error_and_no_data()
    {
        var response = ApiResponse<object>.Failure("VALIDATION_ERROR", "Invalid request.");

        Assert.False(response.Ok);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("VALIDATION_ERROR", response.Error.Code);
    }
}
