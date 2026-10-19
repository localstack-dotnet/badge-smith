#pragma warning disable CA1873 // Logger mocks use expression trees that trigger logging analyzer.

using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests;

public static class TestBase
{
    public static Mock<ILogger<TService>> SetupILoggerWithService<TService>(bool isEnabled = true)
    {
        var mockFor = new Mock<ILogger<TService>>();

        mockFor.Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>())).Returns(isEnabled);

        mockFor.Setup(logger => logger.Log(It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((_, _) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((_, _) => true)));

        return mockFor;
    }

    public static Mock<ILogger> SetupILogger(bool isEnabled = true)
    {
        var mockFor = new Mock<ILogger>();

        mockFor.Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>())).Returns(isEnabled);

        mockFor.Setup(logger => logger.Log(It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((_, _) => true),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((_, _) => true)));

        return mockFor;
    }

    public static Mock<ILogger<TService>> VerifyLogging<TService>(Mock<ILogger<TService>> logger,
        string? expectedMessage = null,
        LogLevel expectedLogLevel = LogLevel.Debug,
        Times? times = null)
    {
        times ??= Times.Once();

        Func<object?, Type, bool> state;

        if (!string.IsNullOrEmpty(expectedMessage))
        {
            state = (v, _) => string.Equals(v?.ToString(), expectedMessage, StringComparison.Ordinal);
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

    public static Mock<ILogger<TService>> VerifyStructuredLogging<TService>(Mock<ILogger<TService>> logger,
        EventId expectedEventId,
        IReadOnlyList<KeyValuePair<string, object?>> expectedState,
        LogLevel expectedLogLevel = LogLevel.Debug)
    {
        var invocation = Assert.Single(logger.Invocations, call => call.Method.Name == nameof(ILogger.Log));
        Assert.Equal(expectedLogLevel, Assert.IsType<LogLevel>(invocation.Arguments[0]));
        var eventId = Assert.IsType<EventId>(invocation.Arguments[1]);
        Assert.Equal(expectedEventId.Id, eventId.Id);
        Assert.Equal(expectedEventId.Name, eventId.Name);
        var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(invocation.Arguments[2]);
        Assert.Equal(expectedState, state);
        Assert.Null(invocation.Arguments[3]);

        return logger;
    }
}

#pragma warning restore CA1873
