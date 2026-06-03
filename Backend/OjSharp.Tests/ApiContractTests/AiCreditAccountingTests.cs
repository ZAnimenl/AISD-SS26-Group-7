using Backend.Api;
using Backend.Domain;

namespace OjSharp.Tests.ApiContractTests;

public sealed class AiCreditAccountingTests
{
    [Fact]
    public void Deduct_credits_reduces_remaining_balance()
    {
        var state = new WorkspaceQuestionState { AiCreditsRemaining = 6 };

        var ok = AiEndpoints.TryDeductCredits(state, AiHintLevels.DefaultCost(AiHintLevels.DebuggingHint), out var remaining);

        Assert.True(ok);
        Assert.Equal(3, remaining);
        Assert.Equal(3, state.AiCreditsRemaining);
    }

    [Fact]
    public void Deduct_credits_rejects_insufficient_balance_without_mutating()
    {
        var state = new WorkspaceQuestionState { AiCreditsRemaining = 2 };

        var ok = AiEndpoints.TryDeductCredits(state, AiHintLevels.DefaultCost(AiHintLevels.PseudocodeHint), out var remaining);

        Assert.False(ok);
        Assert.Equal(2, remaining);
        Assert.Equal(2, state.AiCreditsRemaining);
    }

    [Fact]
    public void Zero_cost_hint_does_not_mutate_balance()
    {
        var state = new WorkspaceQuestionState { AiCreditsRemaining = 2 };

        var ok = AiEndpoints.TryDeductCredits(state, 0, out var remaining);

        Assert.True(ok);
        Assert.Equal(2, remaining);
        Assert.Equal(2, state.AiCreditsRemaining);
    }
}
