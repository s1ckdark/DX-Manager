package io.github.mazemei.dxdisplaycleanup;

import java.util.UUID;

// Retained across Activity recreation; contains no Activity, Context or URI grants.
final class ShareTransferRequestState {
    final String requestId;
    private boolean handled;

    ShareTransferRequestState(String requestId, boolean handled) {
        this.requestId = requestId == null || requestId.isEmpty()
                ? UUID.randomUUID().toString() : requestId;
        this.handled = handled;
    }

    synchronized boolean isHandled() { return handled; }

    synchronized boolean tryHandle(boolean ownerActive) {
        if (!ownerActive || handled) return false;
        handled = true;
        return true;
    }
}
