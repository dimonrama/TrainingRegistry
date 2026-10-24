using TrainingRegistry.Domain;
using Xunit;

namespace TrainingRegistry.Tests;

public class SnilsTests
{
    [Theory]
    [InlineData("112-233-445 95")]
    [InlineData("11223344595")]
    [InlineData("112 233 445 95")]
    public void Valid_number_in_any_notation_passes(string value)
        => Assert.Null(Snils.Validate(value));

    [Fact]
    public void Wrong_control_sum_is_rejected()
        => Assert.NotNull(Snils.Validate("112-233-445 96"));

    [Theory]
    [InlineData("123")]
    [InlineData("1122334459")]
    [InlineData("112-233-445 9X")]
    public void Wrong_length_or_symbols_are_rejected(string value)
        => Assert.NotNull(Snils.Validate(value));

    [Fact]
    public void Normalize_gives_standard_notation()
        => Assert.Equal("112-233-445 95", Snils.Normalize("11223344595"));

    [Fact]
    public void Old_numbers_without_control_sum_are_accepted()
        => Assert.Null(Snils.Validate("001-001-998 00"));

    [Theory]
    [InlineData(1)]
    [InlineData(12345)]
    [InlineData(987654)]
    public void Generated_test_numbers_are_valid(int seed)
        => Assert.Null(Snils.Validate(Snils.FromBase(seed)));
}
