using Xunit;

namespace DexManager.ViewModels.Tests;

public class ImmediateUiDispatcherTests
{
    [Fact]
    public void Post_RunsSynchronouslyAndCounts()
    {
        var dispatcher = new ImmediateUiDispatcher();
        var ran = false;

        dispatcher.Post(() => ran = true);

        Assert.True(ran);
        Assert.Equal(1, dispatcher.PostCount);
    }

    [Fact]
    public async Task InvokeAsync_AwaitsTheAction()
    {
        var dispatcher = new ImmediateUiDispatcher();
        var ran = false;

        await dispatcher.InvokeAsync(async () =>
        {
            await Task.Yield();
            ran = true;
        });

        Assert.True(ran);
        Assert.Equal(1, dispatcher.PostCount);
    }

    [Fact]
    public void Post_NullAction_DoesNotThrow()
    {
        var dispatcher = new ImmediateUiDispatcher();

        dispatcher.Post(null);

        Assert.Equal(1, dispatcher.PostCount);
    }
}
