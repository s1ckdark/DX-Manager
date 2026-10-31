package io.github.mazemei.dxdisplaycleanup;

import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

// Service requests include executor jobs that have not started yet. Their
// terminal cleanup cannot depend on the Runnable's finally being executed.
final class TransferRequestTracker<T> {
    static final class Request<T> {
        final String historyId;
        final List<T> uris;
        private volatile boolean canceled;
        private volatile boolean finished;

        Request(String historyId, List<T> uris) {
            this.historyId = historyId;
            this.uris = new ArrayList<>(uris);
        }

        void cancel() { canceled = true; }
        boolean isCanceled() { return canceled; }
        boolean isFinished() { return finished; }
    }

    static final class Completion<T> {
        final Request<T> request;
        final List<T> releaseUris;
        Completion(Request<T> request, List<T> releaseUris) {
            this.request = request;
            this.releaseUris = releaseUris;
        }
    }

    private final Set<Request<T>> pending = new LinkedHashSet<>();
    private final Set<T> grantedUris = new LinkedHashSet<>();
    private boolean stopped;

    synchronized Request<T> register(String historyId, List<T> uris) {
        if (stopped) return null;
        Request<T> request = new Request<>(historyId, uris);
        pending.add(request);
        grantedUris.addAll(uris);
        return request;
    }

    synchronized Completion<T> complete(Request<T> request) {
        if (!pending.remove(request)) return null;
        request.finished = true;
        List<T> release = new ArrayList<>();
        if (pending.isEmpty()) {
            release.addAll(grantedUris);
            grantedUris.clear();
        }
        return new Completion<>(request, release);
    }

    synchronized List<Completion<T>> stop() {
        stopped = true;
        List<Completion<T>> results = new ArrayList<>();
        for (Request<T> request : new ArrayList<>(pending)) {
            request.cancel();
            results.add(complete(request));
        }
        return results;
    }

    synchronized boolean isEmpty() { return pending.isEmpty(); }
    synchronized boolean isStopped() { return stopped; }
}
