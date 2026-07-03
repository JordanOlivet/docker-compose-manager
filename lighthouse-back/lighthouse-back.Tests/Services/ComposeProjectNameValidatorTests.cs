using Lighthouse.Utils;
using FluentAssertions;

namespace Lighthouse.Tests.Services;

public class ComposeProjectNameValidatorTests
{
    [Theory]
    [InlineData("myproject")]
    [InlineData("my-project")]
    [InlineData("my_project")]
    [InlineData("my.project")]
    [InlineData("Project1")]
    [InlineData("a")]
    [InlineData("123abc")]
    public void IsValid_AcceptsSafeNames(string name)
    {
        ComposeProjectNameValidator.IsValid(name).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-startsWithDash")]
    [InlineData(".startsWithDot")]
    [InlineData("has space")]
    [InlineData("has\"quote")]
    [InlineData("has'quote")]
    [InlineData("has;semicolon")]
    [InlineData("has|pipe")]
    [InlineData("has/slash")]
    [InlineData("has\\backslash")]
    [InlineData("has$dollar")]
    [InlineData("has`backtick")]
    [InlineData("has\nnewline")]
    [InlineData("has&amp")]
    public void IsValid_RejectsUnsafeNames(string? name)
    {
        ComposeProjectNameValidator.IsValid(name).Should().BeFalse();
    }

    [Fact]
    public void IsValid_RejectsOverlyLongNames()
    {
        var longName = new string('a', ComposeProjectNameValidator.MaxLength + 1);
        ComposeProjectNameValidator.IsValid(longName).Should().BeFalse();
    }

    [Fact]
    public void IsValid_AcceptsNameAtMaxLength()
    {
        var maxName = new string('a', ComposeProjectNameValidator.MaxLength);
        ComposeProjectNameValidator.IsValid(maxName).Should().BeTrue();
    }
}
