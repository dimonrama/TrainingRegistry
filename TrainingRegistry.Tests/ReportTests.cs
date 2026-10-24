using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using Xunit;

namespace TrainingRegistry.Tests;

public class ReportTests
{
    private static ReportRow Row(string program, string? org, EnrollmentStatus status, FundingSource? funding,
        DateOnly start, string last = "Иванов")
        => new(1, 1, last, "Пётр", null, null, "112-233-445 95", null, null, org, null, program, "Группа", null, start, null,
            status, funding, null, null, null, null, null, null);

    private static readonly DateOnly Oct = new(2026, 10, 5);

    [Fact]
    public void Summary_by_program_counts_statuses_and_funding()
    {
        var rows = new[]
        {
            Row("А", "Орг1", EnrollmentStatus.Completed, FundingSource.StateTask, Oct),
            Row("А", "Орг2", EnrollmentStatus.Studying, FundingSource.Business, Oct),
            Row("А", "Орг2", EnrollmentStatus.Expelled, null, Oct),
            Row("Б", "Орг1", EnrollmentStatus.Completed, FundingSource.StateTask, Oct)
        };

        var lines = ReportSummary.ByProgram(rows);

        var a = lines.Single(l => l.Key == "А");
        Assert.Equal(3, a.Total);
        Assert.Equal(1, a.Completed);
        Assert.Equal(1, a.Studying);
        Assert.Equal(1, a.Expelled);
        Assert.Equal(1, a.StateTask);
        Assert.Equal(1, a.Business);
        Assert.Equal(1, a.NoFunding);
        Assert.Equal(1, lines.Single(l => l.Key == "Б").Total);
    }

    [Fact]
    public void Summary_by_month_is_ordered_by_date_not_by_text()
    {
        var rows = new[]
        {
            Row("А", null, EnrollmentStatus.Completed, null, new DateOnly(2026, 11, 1)),
            Row("А", null, EnrollmentStatus.Completed, null, new DateOnly(2026, 2, 1))
        };

        var keys = ReportSummary.ByMonth(rows).Select(l => l.Key).ToList();

        Assert.Equal(new[] { "февраль 2026", "ноябрь 2026" }, keys);
    }

    [Fact]
    public void People_without_organization_are_grouped_separately()
    {
        var lines = ReportSummary.ByOrganization(new[] { Row("А", null, EnrollmentStatus.Studying, null, Oct) });

        Assert.Equal("Физическое лицо", lines.Single().Key);
    }

    [Fact]
    public void Personal_fields_are_hidden_without_permission()
    {
        var keys = new[] { "name", "snils", "birth", "org" };

        var limited = ReportFields.Select(keys, allowPersonal: false).Select(f => f.Key).ToList();
        var full = ReportFields.Select(keys, allowPersonal: true).Select(f => f.Key).ToList();

        Assert.Equal(new[] { "name", "org" }, limited);
        Assert.Contains("snils", full);
    }

    [Fact]
    public void Unknown_field_keys_are_ignored()
        => Assert.Empty(ReportFields.Select(new[] { "nope" }, true));

    [Fact]
    public void Default_selection_is_used_when_nothing_chosen()
        => Assert.NotEmpty(ReportFields.Select(null, false));

    [Fact]
    public void Snils_mask_leaves_only_last_two_digits()
        => Assert.Equal("***-***-*** 95", ReportFields.MaskSnils("112-233-445 95"));

    [Fact]
    public void Mask_of_empty_snils_is_null()
        => Assert.Null(ReportFields.MaskSnils(null));

    [Fact]
    public void Filter_rejects_reversed_period()
        => Assert.NotNull(new ReportFilter { From = new DateOnly(2026, 5, 1), To = new DateOnly(2026, 4, 1) }.Validate());
}
