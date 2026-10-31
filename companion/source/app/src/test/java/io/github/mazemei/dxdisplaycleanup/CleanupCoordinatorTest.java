package io.github.mazemei.dxdisplaycleanup;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public final class CleanupCoordinatorTest {
    @Test
    public void ordinaryCleanupRequiresInactiveRequestedSettings() {
        CleanupCoordinator.Snapshot complete = new CleanupCoordinator.Snapshot(
                new OverlayDisplayRepository.Snapshot(
                        OverlayDisplayRepository.Status.INACTIVE,
                        false, null, null),
                new StayAwakeRepository.Snapshot(
                        StayAwakeRepository.Status.INACTIVE,
                        false, "0", null));
        CleanupCoordinator.Snapshot failedOverlay = new CleanupCoordinator.Snapshot(
                new OverlayDisplayRepository.Snapshot(
                        OverlayDisplayRepository.Status.ERROR,
                        true, "1600x900/150", "denied"),
                complete.stayAwake);

        assertTrue(CleanupCoordinator.isCleanupComplete(
                complete, true, true));
        assertFalse(CleanupCoordinator.isCleanupComplete(
                failedOverlay, true, true));
    }

    @Test
    public void windowsShutdownAcceptsTheOriginalNonzeroStayAwakeValue() {
        CleanupCoordinator.Snapshot restoredSeven = new CleanupCoordinator.Snapshot(
                new OverlayDisplayRepository.Snapshot(
                        OverlayDisplayRepository.Status.INACTIVE,
                        false, null, null),
                new StayAwakeRepository.Snapshot(
                        StayAwakeRepository.Status.ACTIVE,
                        true, "7", null));
        CleanupCoordinator.Snapshot wrongValue = new CleanupCoordinator.Snapshot(
                restoredSeven.overlay,
                new StayAwakeRepository.Snapshot(
                        StayAwakeRepository.Status.INACTIVE,
                        false, "0", null));

        assertTrue(CleanupCoordinator.isWindowsShutdownCleanupComplete(
                restoredSeven, true, true, "7"));
        assertFalse(CleanupCoordinator.isWindowsShutdownCleanupComplete(
                wrongValue, true, true, "7"));
    }
}
