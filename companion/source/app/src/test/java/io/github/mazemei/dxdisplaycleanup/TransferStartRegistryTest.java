package io.github.mazemei.dxdisplaycleanup;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.atomic.AtomicInteger;
import org.junit.Test;
import static org.junit.Assert.*;

public final class TransferStartRegistryTest {
    @Test public void duplicateCallbackEnqueuesOnlyOnceEvenAfterCompletion() {
        TransferStartRegistry starts = new TransferStartRegistry(16);
        AtomicInteger enqueued = new AtomicInteger();
        assertEquals(TransferStartRegistry.Result.STARTED,
                starts.start("share-1", () -> { enqueued.incrementAndGet(); return true; }));
        assertEquals(TransferStartRegistry.Result.ALREADY_STARTED,
                starts.start("share-1", () -> { enqueued.incrementAndGet(); return true; }));
        assertEquals(1, enqueued.get());
    }

    @Test public void deliberateResendOfSameContentIsNotSuppressed() {
        TransferStartRegistry starts = new TransferStartRegistry(16);
        AtomicInteger enqueued = new AtomicInteger();
        for (String id : new String[] { "first-share", "second-share" }) {
            assertEquals(TransferStartRegistry.Result.STARTED,
                    starts.start(id, () -> { enqueued.incrementAndGet(); return true; }));
        }
        assertEquals(2, enqueued.get());
    }

    @Test public void failedEnqueueDoesNotReserveTheRequest() {
        TransferStartRegistry starts = new TransferStartRegistry(16);
        assertEquals(TransferStartRegistry.Result.FAILED, starts.start("share-1", () -> false));
        assertEquals(TransferStartRegistry.Result.STARTED, starts.start("share-1", () -> true));
    }

    @Test public void concurrentCallbacksCannotStartTwoJobs() throws Exception {
        TransferStartRegistry starts = new TransferStartRegistry(16);
        AtomicInteger enqueued = new AtomicInteger();
        ExecutorService pool = Executors.newFixedThreadPool(4);
        try {
            List<Future<TransferStartRegistry.Result>> results = new ArrayList<>();
            for (int i = 0; i < 24; i++) {
                results.add(pool.submit(() -> starts.start("share-1", () -> {
                    enqueued.incrementAndGet(); return true;
                })));
            }
            int started = 0;
            for (Future<TransferStartRegistry.Result> result : results)
                if (result.get() == TransferStartRegistry.Result.STARTED) started++;
            assertEquals(1, started);
            assertEquals(1, enqueued.get());
        } finally { pool.shutdownNow(); }
    }
}
