using Infrastructure.Scheduling;
using Quartz;

namespace Infrastructure.Tests.Scheduling;

public sealed class QuartzCronExpressionTests
{
    [Theory]
    [InlineData("*/5 * * * *", "0 */5 * * * ?")]
    [InlineData("*/10 * * * *", "0 */10 * * * ?")]
    [InlineData("0 3 * * *", "0 0 3 * * ?")]
    [InlineData("30 2 * * *", "0 30 2 * * ?")]
    public void ToQuartz_converts_standard_five_field_expressions(string unix, string expected)
    {
        string quartz = QuartzCronExpression.ToQuartz(unix);
        Assert.Equal(expected, quartz);
        CronExpression.ValidateExpression(quartz);
    }

    [Fact]
    public void ToQuartz_rejects_empty_expression()
    {
        Assert.Throws<FormatException>(() => QuartzCronExpression.ToQuartz("   "));
    }
}
