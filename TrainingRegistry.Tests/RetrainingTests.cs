using TrainingRegistry.Domain;
using Xunit;

namespace TrainingRegistry.Tests;

public class RetrainingTests
{
    [Theory]
    [InlineData(Recurrence.OneYear, null, "2026-10-01", "2027-10-01")]
    [InlineData(Recurrence.ThreeYears, null, "2026-10-01", "2029-10-01")]
    [InlineData(Recurrence.FiveYears, null, "2026-10-01", "2031-10-01")]
    [InlineData(Recurrence.Custom, 18, "2026-08-31", "2028-02-29")]
    [InlineData(Recurrence.OneYear, null, "2024-02-29", "2025-02-28")]
    public void NextTrainingDate_is_completion_plus_recurrence(Recurrence recurrence, int? months, string completed, string expected)
    {
        var program = new TrainingProgram { Recurrence = recurrence, CustomMonths = months };

        var next = program.CalculateNextTrainingDate(DateOnly.Parse(completed));

        Assert.Equal(DateOnly.Parse(expected), next);
    }

    [Fact]
    public void NextTrainingDate_is_null_when_no_recurrence()
    {
        var program = new TrainingProgram { Recurrence = Recurrence.None };

        Assert.Null(program.CalculateNextTrainingDate(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void MarkCompleted_sets_status_and_dates()
    {
        var program = new TrainingProgram { Recurrence = Recurrence.ThreeYears };
        var enrollment = new Enrollment();

        enrollment.MarkCompleted(new DateOnly(2026, 10, 1), program);

        Assert.Equal(EnrollmentStatus.Completed, enrollment.Status);
        Assert.Equal(new DateOnly(2026, 10, 1), enrollment.CompletedDate);
        Assert.Equal(new DateOnly(2029, 10, 1), enrollment.NextTrainingDate);
    }

    [Theory]
    [InlineData(-5, 60, Urgency.Red)]
    [InlineData(0, 60, Urgency.Red)]
    [InlineData(60, 60, Urgency.Red)]
    [InlineData(61, 60, Urgency.Yellow)]
    [InlineData(89, 60, Urgency.Yellow)]
    [InlineData(90, 60, Urgency.Green)]
    [InlineData(100, 90, Urgency.Yellow)]
    public void Urgency_follows_program_threshold(int daysLeft, int threshold, Urgency expected)
        => Assert.Equal(expected, RetrainingRules.GetUrgency(daysLeft, threshold));

    [Fact]
    public void Default_notify_threshold_is_60_days()
        => Assert.Equal(60, new TrainingProgram().EffectiveNotifyDays);

    [Theory]
    [InlineData(EducationLevel.Higher, RequiredEducation.SecondaryVocational, true)]
    [InlineData(EducationLevel.Higher, RequiredEducation.Higher, true)]
    [InlineData(EducationLevel.SecondaryVocational, RequiredEducation.Higher, false)]
    [InlineData(EducationLevel.SecondaryGeneral, RequiredEducation.SecondaryVocational, false)]
    [InlineData(EducationLevel.Other, RequiredEducation.None, true)]
    public void Education_satisfies_requirement(EducationLevel have, RequiredEducation required, bool expected)
        => Assert.Equal(expected, EducationRules.Satisfies(have, required));
}

public class ContractTests
{
    [Theory]
    [InlineData(0, "Ноль рублей 00 копеек")]
    [InlineData(1, "Один рубль 00 копеек")]
    [InlineData(4500, "Четыре тысячи пятьсот рублей 00 копеек")]
    [InlineData(21001, "Двадцать одна тысяча один рубль 00 копеек")]
    [InlineData(122000, "Сто двадцать две тысячи рублей 00 копеек")]
    public void Amount_in_words(int rubles, string expected)
        => Assert.Equal(expected, TrainingRegistry.Infrastructure.RuWords.Rubles(rubles));

    [Fact]
    public void Amount_in_words_with_kopecks()
        => Assert.Equal("Тринадцать тысяч пятьсот рублей 01 копейка", TrainingRegistry.Infrastructure.RuWords.Rubles(13500.01m));

    [Fact]
    public void Docx_is_a_valid_zip_with_main_part_and_escapes_xml()
    {
        var model = new TrainingRegistry.Infrastructure.Documents.ContractDocModel(
            "Д-2026-0001", new DateOnly(2026, 10, 7), "Охрана труда", "40 часов", "Группа 1",
            new DateOnly(2026, 10, 1), null, 4500m, 1, 4500m,
            new TrainingRegistry.Infrastructure.Documents.CustomerInfo("ООО «Ромашка» & Co <тест>", true, "7701234567",
                null, null, null, null, null, null, null),
            new[] { new TrainingRegistry.Infrastructure.Documents.StudentInfo("Петров Пётр", null) });

        var blocks = TrainingRegistry.Infrastructure.Documents.ContractBlocks.Build(
            model, new TrainingRegistry.Infrastructure.Documents.ContractSettings());
        var bytes = TrainingRegistry.Infrastructure.Documents.DocxRenderer.Render(blocks);

        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        var entry = zip.GetEntry("word/document.xml");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("Д-2026-0001", xml);
        Assert.Contains("&amp; Co &lt;тест&gt;", xml);
    }
}

public class ExportAndSecurityTests
{
    [Fact]
    public void Xlsx_contains_sheet_with_headers_and_rows()
    {
        var bytes = TrainingRegistry.Infrastructure.XlsxWriter.Create("Лист",
            new[] { "Название", "Дата" },
            new[] { new object?[] { "ООО «А» & <Б>", new DateOnly(2026, 10, 8) } });

        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet!.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("Название", xml);
        Assert.Contains("&amp; &lt;Б&gt;", xml);
    }

    [Fact]
    public void Auth_stamp_changes_with_role_permissions_and_password()
    {
        var baseStamp = TrainingRegistry.Security.AuthStamp.Compute(UserRole.Methodist, false, false, "hash1");
        Assert.Equal(baseStamp, TrainingRegistry.Security.AuthStamp.Compute(UserRole.Methodist, false, false, "hash1"));
        Assert.NotEqual(baseStamp, TrainingRegistry.Security.AuthStamp.Compute(UserRole.Admin, false, false, "hash1"));
        Assert.NotEqual(baseStamp, TrainingRegistry.Security.AuthStamp.Compute(UserRole.Methodist, true, false, "hash1"));
        Assert.NotEqual(baseStamp, TrainingRegistry.Security.AuthStamp.Compute(UserRole.Methodist, false, true, "hash1"));
        Assert.NotEqual(baseStamp, TrainingRegistry.Security.AuthStamp.Compute(UserRole.Methodist, false, false, "hash2"));
    }

    [Fact]
    public void Audit_names_are_translated_and_unknown_kept()
    {
        Assert.Equal("Договор", AuditNames.EntityType("Contract"));
        Assert.Equal("Unknown", AuditNames.EntityType("Unknown"));
    }
}

public class DemoDataRuleTests
{
    [Theory]
    [InlineData(14, Urgency.Red)]
    [InlineData(60, Urgency.Red)]
    [InlineData(75, Urgency.Yellow)]
    [InlineData(150, Urgency.Green)]
    public void Completion_date_calculated_backwards_gives_expected_urgency(int daysLeft, Urgency expected)
    {
        var today = new DateOnly(2026, 10, 9);
        var program = new TrainingProgram { Recurrence = Recurrence.OneYear };

        var completed = today.AddDays(daysLeft).AddMonths(-12);
        var next = program.CalculateNextTrainingDate(completed)!.Value;
        var left = next.DayNumber - today.DayNumber;

        Assert.InRange(left, daysLeft - 3, daysLeft + 3);
        Assert.Equal(expected, RetrainingRules.GetUrgency(left, 60));
    }
}
