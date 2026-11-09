using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.Refunds;

namespace WaterbusSystem.UnitTests.TestHelpers;

internal static class RefundTestOptions
{
    internal static IOptions<RefundOptions> Gateway => Options.Create(new RefundOptions { Mode = RefundMode.Gateway });
    internal static IOptions<RefundOptions> Demo => Options.Create(new RefundOptions { Mode = RefundMode.Demo });
}
