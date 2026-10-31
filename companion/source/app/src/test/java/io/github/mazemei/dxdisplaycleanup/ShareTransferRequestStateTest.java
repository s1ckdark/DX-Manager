package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;
import static org.junit.Assert.*;

public final class ShareTransferRequestStateTest {
    @Test public void destroyedOwnerCannotConsumeTheRecreatedRequest() {
        ShareTransferRequestState state = new ShareTransferRequestState(null, false);
        String originalId = state.requestId;
        ShareTransferRequestState retained = state;
        assertFalse(state.tryHandle(false));
        assertFalse(retained.isHandled());
        assertTrue(retained.tryHandle(true));
        assertFalse(state.tryHandle(true));
        assertEquals(originalId, retained.requestId);
    }

    @Test public void handledRequestRestoresWithoutEnqueueingAgain() {
        ShareTransferRequestState initial = new ShareTransferRequestState(null, false);
        assertTrue(initial.tryHandle(true));
        ShareTransferRequestState restored = new ShareTransferRequestState(initial.requestId, initial.isHandled());
        assertTrue(restored.isHandled());
        assertFalse(restored.tryHandle(true));
        assertEquals(initial.requestId, restored.requestId);
    }

    @Test public void unhandledRestoreCanStillSendOnce() {
        ShareTransferRequestState initial = new ShareTransferRequestState(null, false);
        ShareTransferRequestState restored = new ShareTransferRequestState(initial.requestId, false);
        assertTrue(restored.tryHandle(true));
        assertFalse(restored.tryHandle(true));
    }

    @Test public void deliberateSecondShareHasANewRequestId() {
        ShareTransferRequestState first = new ShareTransferRequestState(null, false);
        ShareTransferRequestState second = new ShareTransferRequestState(null, false);
        assertNotEquals(first.requestId, second.requestId);
        assertTrue(first.tryHandle(true));
        assertTrue(second.tryHandle(true));
    }
}
