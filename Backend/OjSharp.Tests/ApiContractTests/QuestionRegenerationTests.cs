using Backend.Api;
using Backend.Domain;

namespace OjSharp.Tests.ApiContractTests;

public sealed class QuestionRegenerationTests
{
    [Fact]
    public void Material_difference_rejects_unchanged_title_and_problem_ignoring_spacing_and_case()
    {
        var candidate = new Question
        {
            Title = "  PRODUCT   PRICE FILTER ",
            ProblemDescriptionMarkdown = "Filter products by price."
        };

        var isDifferent = QuestionEndpoints.IsMateriallyDifferent(
            candidate,
            [("Product Price Filter", "  FILTER products BY price. ")]);

        Assert.False(isDifferent);
    }

    [Fact]
    public void Material_difference_accepts_a_changed_title_or_problem()
    {
        var candidate = new Question
        {
            Title = "Accessible Product Sorter",
            ProblemDescriptionMarkdown = "Sort products by price and name."
        };

        var isDifferent = QuestionEndpoints.IsMateriallyDifferent(
            candidate,
            [("Product Price Filter", "Filter products by price.")]);

        Assert.True(isDifferent);
    }
}
