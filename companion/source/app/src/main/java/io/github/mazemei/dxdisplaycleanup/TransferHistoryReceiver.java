package io.github.mazemei.dxdisplaycleanup;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Base64;

import java.nio.charset.StandardCharsets;

public final class TransferHistoryReceiver extends BroadcastReceiver {
    public static final String ACTION_RECORD =
            "io.github.mazemei.dxdisplaycleanup.RECORD_TRANSFER_HISTORY";

    @Override
    public void onReceive(Context context, Intent intent) {
        if (intent == null || !ACTION_RECORD.equals(intent.getAction())) {
            return;
        }
        String id = decode(intent.getStringExtra("id64"));
        String name = decode(intent.getStringExtra("name64"));
        if (id.isEmpty()) {
            return;
        }
        TransferHistoryStore.recordPcToPhone(context, id, name,
                intent.getIntExtra("count", 1),
                intent.getBooleanExtra("folder", false),
                intent.getStringExtra("status"),
                intent.getLongExtra("time", System.currentTimeMillis()));
    }

    private static String decode(String value) {
        if (value == null || value.isEmpty()) {
            return "";
        }
        try {
            return new String(Base64.decode(value, Base64.DEFAULT),
                    StandardCharsets.UTF_8);
        } catch (IllegalArgumentException ignored) {
            return "";
        }
    }
}
