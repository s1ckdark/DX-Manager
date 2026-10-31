package io.github.mazemei.dxdisplaycleanup;

final class GuardianIdleStopPolicy {
    static final long STARTUP_GRACE_MS = 5000L;

    private GuardianIdleStopPolicy() {
    }

    static long startupDelayRemaining(boolean hadConnection, long elapsedMs) {
        return hadConnection ? 0L
                : Math.max(0L, STARTUP_GRACE_MS - Math.max(0L, elapsedMs));
    }

    static boolean shouldStop(boolean connected, boolean hadConnection,
                              long elapsedMs, CleanupCoordinator.Snapshot snapshot) {
        return !connected && startupDelayRemaining(hadConnection, elapsedMs) == 0L
                && CleanupCoordinator.isCleanupComplete(snapshot, true, true);
    }
}
