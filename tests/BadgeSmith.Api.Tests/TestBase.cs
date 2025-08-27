#pragma warning disable S2325

using Microsoft.Extensions.Logging;
using Moq;

namespace BadgeSmith.Api.Tests;

public class TestBase
{
    public Mock<ILogger<TService>> SetupILoggerWithService<TService>()
    {
        var mockFor = new Mock<ILogger<TService>>();

        mockFor.Setup(logger => logger.Log(It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((_, _) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((_, _) => true)));

        return mockFor;
    }

    public Mock<ILogger> SetupILogger()
    {
        var mockFor = new Mock<ILogger>();

        mockFor.Setup(logger => logger.Log(It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((_, _) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((_, _) => true)));

        return mockFor;
    }

    public Mock<ILogger<TService>> VerifyLogging<TService>(Mock<ILogger<TService>> logger,
        string? expectedMessage = null,
        LogLevel expectedLogLevel = LogLevel.Debug,
        Times? times = null)
    {
        times ??= Times.Once();

        Func<object, Type, bool> state;

        if (!string.IsNullOrEmpty(expectedMessage))
        {
            state = (v, _) => string.Equals(v.ToString(), expectedMessage, StringComparison.Ordinal);
        }
        else
        {
            state = (_, _) => true;
        }

        logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == expectedLogLevel),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => state(v, t)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((_, _) => true)), (Times)times);

        return logger;
    }
}
