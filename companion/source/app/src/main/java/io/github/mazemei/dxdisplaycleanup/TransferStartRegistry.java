package io.github.mazemei.dxdisplaycleanup;

import java.util.LinkedHashSet;
import java.util.Set;
import java.util.function.BooleanSupplier;

// Dedupe the request ID, never the content: a deliberate second share remains valid.
// Lives in the app process, so a late Activity callback cannot restart a finished service job.
final class TransferStartRegistry {
    enum Result { STARTED, ALREADY_STARTED, FAILED }
    private final Set<String> accepted = new LinkedHashSet<>();
    private final int limit;

    TransferStartRegistry(int limit) {
        if (limit < 1) throw new IllegalArgumentException("limit");
        this.limit = limit;
    }

    synchronized Result start(String requestId, BooleanSupplier enqueue) {
        if (requestId == null || requestId.isEmpty()) throw new IllegalArgumentException("requestId");
        if (accepted.contains(requestId)) return Result.ALREADY_STARTED;
        if (!enqueue.getAsBoolean()) return Result.FAILED;
        accepted.add(requestId);
        if (accepted.size() > limit) accepted.remove(accepted.iterator().next());
        return Result.STARTED;
    }
}
