using CarAutoParts.Application.Services;
using Moq;

namespace CarAutoParts.Application.Tests;

internal static class OpsSlaTestDoubles
{
    public static IOpsSlaClockService NoOp => Mock.Of<IOpsSlaClockService>();
}
