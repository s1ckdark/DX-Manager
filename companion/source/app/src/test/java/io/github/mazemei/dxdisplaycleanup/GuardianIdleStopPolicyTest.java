package io.github.mazemei.dxdisplaycleanup;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public final class GuardianIdleStopPolicyTest {
    private CleanupCoordinator.Snapshot inactive() {
        return new CleanupCoordinator.Snapshot(
                new OverlayDisplayRepository.Snapshot(
                        OverlayDisplayRepository.Status.INACTIVE, false, null, null),
                new StayAwakeRepository.Snapshot(
                        StayAwakeRepository.Status.INACTIVE, false, "0", null));
    }

    @Test
    public void authenticatedDisconnectStopsImmediatelyAfterCleanup() {
        assertTrue(GuardianIdleStopPolicy.shouldStop(false, true, 0, inactive()));
    }

    @Test
    public void connectedPcKeepsShutdownProtectionEvenWithoutManagedWindows() {
        assertFalse(GuardianIdleStopPolicy.shouldStop(true, true, 10000, inactive()));
    }

    @Test
    public void initialTunnelHasOneFixedGracePeriod() {
        assertFalse(GuardianIdleStopPolicy.shouldStop(false, false, 0, inactive()));
        assertFalse(GuardianIdleStopPolicy.shouldStop(false, false, 4999, inactive()));
        assertTrue(GuardianIdleStopPolicy.shouldStop(false, false, 5000, inactive()));
        assertEquals(3000, GuardianIdleStopPolicy.startupDelayRemaining(false, 2000));
        assertEquals(0, GuardianIdleStopPolicy.startupDelayRemaining(false, 7000));
        assertEquals(0, GuardianIdleStopPolicy.startupDelayRemaining(true, 0));
    }

    @Test
    public void remainingVirtualDisplayRetainsRecovery() {
        CleanupCoordinator.Snapshot snapshot = new CleanupCoordinator.Snapshot(
                new OverlayDisplayRepository.Snapshot(
                        OverlayDisplayRepository.Status.ACTIVE, true, "1600x900/150", null),
                inactive().stayAwake);
        assertFalse(GuardianIdleStopPolicy.shouldStop(false, true, 10000, snapshot));
    }

    @Test
    public void remainingStayAwakeRetainsRecovery() {
        CleanupCoordinator.Snapshot snapshot = new CleanupCoordinator.Snapshot(
                inactive().overlay, new StayAwakeRepository.Snapshot(
                        StayAwakeRepository.Status.ACTIVE, true, "7", null));
        assertFalse(GuardianIdleStopPolicy.shouldStop(false, true, 10000, snapshot));
    }

    @Test
    public void failedOrUnpermittedInspectionCannotBeTreatedAsClean() {
        for (OverlayDisplayRepository.Status status : new OverlayDisplayRepository.Status[] {
                OverlayDisplayRepository.Status.ERROR,
                OverlayDisplayRepository.Status.PERMISSION_REQUIRED }) {
            CleanupCoordinator.Snapshot snapshot = new CleanupCoordinator.Snapshot(
                    new OverlayDisplayRepository.Snapshot(status, false, null, "unverified"),
                    inactive().stayAwake);
            assertFalse(GuardianIdleStopPolicy.shouldStop(false, true, 10000, snapshot));
        }
        CleanupCoordinator.Snapshot snapshot = new CleanupCoordinator.Snapshot(
                inactive().overlay, new StayAwakeRepository.Snapshot(
                        StayAwakeRepository.Status.ERROR, false, null, "unverified"));
        assertFalse(GuardianIdleStopPolicy.shouldStop(false, true, 10000, snapshot));
        assertFalse(GuardianIdleStopPolicy.shouldStop(false, true, 10000, null));
    }
}
