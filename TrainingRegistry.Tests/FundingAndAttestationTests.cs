using TrainingRegistry.Domain;
using Xunit;

namespace TrainingRegistry.Tests;

public class FundingAndAttestationTests
{
    [Fact]
    public void Own_funding_wins_over_group_funding()
        => Assert.Equal(FundingSource.Business, FundingRules.Effective(FundingSource.StateTask, FundingSource.Business));

    [Fact]
    public void Group_funding_is_used_when_student_has_none()
        => Assert.Equal(FundingSource.StateTask, FundingRules.Effective(FundingSource.StateTask, null));

    [Fact]
    public void Conflict_is_reported_only_when_both_set_and_different()
    {
        Assert.True(FundingRules.Conflicts(FundingSource.StateTask, FundingSource.Business));
        Assert.False(FundingRules.Conflicts(FundingSource.StateTask, FundingSource.StateTask));
        Assert.False(FundingRules.Conflicts(null, FundingSource.Business));
    }

    [Theory]
    [InlineData(FundingSource.StateTask, "ГЗ")]
    [InlineData(FundingSource.Business, "ПД")]
    public void Short_names(FundingSource source, string expected) => Assert.Equal(expected, FundingRules.Short(source));

    [Fact]
    public void Missing_funding_is_shown_as_not_set()
        => Assert.Equal("не указано", FundingRules.Short(null));

    [Theory]
    [InlineData(null, true)]
    [InlineData(AttestationResult.Excellent, true)]
    [InlineData(AttestationResult.Passed, true)]
    [InlineData(AttestationResult.Unsatisfactory, false)]
    [InlineData(AttestationResult.Failed, false)]
    [InlineData(AttestationResult.Absent, false)]
    public void Only_successful_results_go_to_protocol(AttestationResult? result, bool expected)
        => Assert.Equal(expected, AttestationRules.IsSuccessful(result));
}
