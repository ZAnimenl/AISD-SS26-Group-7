using Backend.Services;

namespace OjSharp.Tests.ApiContractTests;

public sealed class CodeEvaluationServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("def solve(arr):\n    pass")]
    [InlineData("function solve(arr) {\n  // TODO\n}")]
    public void Placeholder_code_is_not_meaningful(string code)
    {
        var service = new CodeEvaluationService();

        Assert.False(service.IsMeaningfulCode(code));
    }

    [Fact]
    public void Implemented_code_is_meaningful()
    {
        var service = new CodeEvaluationService();

        Assert.True(service.IsMeaningfulCode("def solve(arr):\n    return sum(arr)\n"));
    }
}
