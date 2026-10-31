package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;
import java.util.Arrays;
import java.util.List;
import static org.junit.Assert.*;

public final class TransferRequestTrackerTest {
    @Test public void completesGrantsOnlyAfterTheWholeQueueDrains() {
        TransferRequestTracker<String> queue = new TransferRequestTracker<>();
        TransferRequestTracker.Request<String> a = queue.register("a", Arrays.asList("shared", "a"));
        TransferRequestTracker.Request<String> b = queue.register("b", Arrays.asList("shared", "b"));
        assertTrue(queue.complete(a).releaseUris.isEmpty());
        assertEquals(Arrays.asList("shared", "a", "b"), queue.complete(b).releaseUris);
        assertTrue(queue.isEmpty());
    }

    @Test public void timeoutTerminatesActiveAndNeverExecutedRequests() {
        TransferRequestTracker<String> queue = new TransferRequestTracker<>();
        TransferRequestTracker.Request<String> a = queue.register("active", Arrays.asList("shared"));
        TransferRequestTracker.Request<String> b = queue.register("queued", Arrays.asList("shared", "b"));
        List<TransferRequestTracker.Completion<String>> cleanup = queue.stop();
        assertEquals(2, cleanup.size());
        assertEquals("active", cleanup.get(0).request.historyId);
        assertEquals("queued", cleanup.get(1).request.historyId);
        assertTrue(a.isCanceled() && b.isCanceled() && a.isFinished() && b.isFinished());
        assertTrue(cleanup.get(0).releaseUris.isEmpty());
        assertEquals(Arrays.asList("shared", "b"), cleanup.get(1).releaseUris);
        assertNull(queue.register("late", Arrays.asList("late")));
    }

    @Test public void workerFinallyCannotRepeatTimeoutCleanup() {
        TransferRequestTracker<String> queue = new TransferRequestTracker<>();
        TransferRequestTracker.Request<String> request = queue.register("a", Arrays.asList("uri"));
        assertEquals(1, queue.stop().size());
        assertNull(queue.complete(request));
        assertTrue(queue.stop().isEmpty());
    }

    @Test public void cancelIsOwnedByOneRequestAndCannotBeReset() {
        TransferRequestTracker<String> queue = new TransferRequestTracker<>();
        TransferRequestTracker.Request<String> a = queue.register("a", Arrays.asList("a"));
        TransferRequestTracker.Request<String> b = queue.register("b", Arrays.asList("b"));
        a.cancel();
        assertTrue(a.isCanceled());
        assertFalse(b.isCanceled());
        queue.stop();
        assertTrue(a.isCanceled() && b.isCanceled());
    }
}
