using Xunit;

namespace DexManager.ViewModels.Tests;

public class QueueingUiDispatcherTests
{
    [Fact]
    public void Post_DoesNotRunUntilDrain()
    {
        var dispatcher = new QueueingUiDispatcher();
        var ran = false;

        dispatcher.Post(() => ran = true);

        Assert.False(ran);
        Assert.Equal(1, dispatcher.PostCount);
        Assert.Equal(1, dispatcher.PendingCount);

        Assert.Equal(1, dispatcher.Drain());

        Assert.True(ran);
        Assert.Equal(0, dispatcher.PendingCount);
    }

    [Fact]
    public void Drain_RunsQueuedActionsInPostOrder()
    {
        var dispatcher = new QueueingUiDispatcher();
        var order = new List<int>();

        dispatcher.Post(() => order.Add(1));
        dispatcher.Post(() => order.Add(2));
        dispatcher.Drain();

        Assert.Equal(new[] { 1, 2 }, order);
    }

    [Fact]
    public void Post_NullAction_CountsButQueuesNothing()
    {
        var dispatcher = new QueueingUiDispatcher();

        dispatcher.Post(null);

        Assert.Equal(1, dispatcher.PostCount);
        Assert.Equal(0, dispatcher.PendingCount);
    }

    [Fact]
    public async Task InvokeAsync_CompletesOnDrain()
    {
        var dispatcher = new QueueingUiDispatcher();
        var ran = false;

        var pending = dispatcher.InvokeAsync(async () =>
        {
            await Task.Yield();
            ran = true;
        });

        Assert.False(ran);
        Assert.False(pending.IsCompleted);

        dispatcher.Drain();
        await pending;

        Assert.True(ran);
    }
}
