package io.github.mazemei.dxdisplaycleanup;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.UUID;

final class TransferHistoryStore {
    static final int MAX_ENTRIES_PER_DIRECTION = 10;
    private static final int MAX_ENTRIES = MAX_ENTRIES_PER_DIRECTION * 2;
    static final String DIRECTION_PHONE_TO_PC = "phone_to_pc";
    static final String DIRECTION_PC_TO_PHONE = "pc_to_phone";
    static final String STATUS_QUEUED = "queued";
    static final String STATUS_TRANSFERRING = "transferring";
    static final String STATUS_COMPLETED = "completed";
    static final String STATUS_FAILED = "failed";
    static final String STATUS_CANCELED = "canceled";

    private static final String PREFERENCES = "transfer_history";
    private static final String KEY_ENTRIES = "entries_v1";
    private static final String KEY_REVISION = "revision";
    private static final Object LOCK = new Object();

    static final class Entry {
        final String id;
        final String direction;
        final String status;
        final String name;
        final int itemCount;
        final boolean folder;
        final long timestamp;

        Entry(String id, String direction, String status, String name,
                int itemCount, boolean folder, long timestamp) {
            this.id = clean(id);
            this.direction = normalizeDirection(direction);
            this.status = normalizeStatus(status);
            this.name = clean(name);
            this.itemCount = Math.max(1, itemCount);
            this.folder = folder;
            this.timestamp = timestamp > 0
                    ? timestamp : System.currentTimeMillis();
        }
    }

    private TransferHistoryStore() {
    }

    static String queuePhoneToPc(Context context) {
        String id = UUID.randomUUID().toString();
        upsert(context, new Entry(id, DIRECTION_PHONE_TO_PC,
                STATUS_QUEUED, "", 1, false,
                System.currentTimeMillis()));
        return id;
    }

    static void updatePhoneToPc(Context context, String id, String status,
            PhoneTransferPlanner.Plan plan) {
        synchronized (LOCK) {
            List<Entry> entries = readLocked(context);
            Entry previous = find(entries, id);
            String name = previous == null ? "" : previous.name;
            int count = previous == null ? 1 : previous.itemCount;
            boolean folder = previous != null && previous.folder;
            if (plan != null && !plan.entries.isEmpty()) {
                Set<Integer> roots = new HashSet<>();
                int fileCount = 0;
                for (PhoneTransferEntry item : plan.entries) {
                    roots.add(item.rootId);
                    if (!item.directory) {
                        fileCount++;
                    }
                }
                PhoneTransferEntry first = plan.entries.get(0);
                name = rootName(first.relativePath);
                folder = roots.size() == 1 && first.directory;
                count = Math.max(1, fileCount > 0 ? fileCount : roots.size());
            }
            putLocked(context, entries, new Entry(id,
                    DIRECTION_PHONE_TO_PC, status, name, count, folder,
                    previous == null ? System.currentTimeMillis()
                            : previous.timestamp));
        }
    }

    static void recordPcToPhone(Context context, String id, String name,
            int itemCount, boolean folder, String status, long timestamp) {
        upsert(context, new Entry(id, DIRECTION_PC_TO_PHONE, status,
                name, itemCount, folder, timestamp));
    }

    static List<Entry> load(Context context) {
        synchronized (LOCK) {
            return new ArrayList<>(readLocked(context));
        }
    }

    static void clear(Context context, String direction) {
        synchronized (LOCK) {
            String normalizedDirection = normalizeDirection(direction);
            List<Entry> entries = readLocked(context);
            boolean changed = false;
            for (int index = entries.size() - 1; index >= 0; index--) {
                if (normalizedDirection.equals(entries.get(index).direction)) {
                    entries.remove(index);
                    changed = true;
                }
            }
            if (changed) {
                writeLocked(context, entries);
            }
        }
    }

    static long revision(Context context) {
        return preferences(context).getLong(KEY_REVISION, 0L);
    }

    private static void upsert(Context context, Entry entry) {
        synchronized (LOCK) {
            List<Entry> entries = readLocked(context);
            putLocked(context, entries, entry);
        }
    }

    private static void putLocked(Context context, List<Entry> entries,
            Entry entry) {
        for (int index = entries.size() - 1; index >= 0; index--) {
            if (entries.get(index).id.equals(entry.id)) {
                entries.remove(index);
            }
        }
        entries.add(0, entry);
        int directionCount = 0;
        for (int index = 0; index < entries.size(); index++) {
            if (!entry.direction.equals(entries.get(index).direction)) {
                continue;
            }
            directionCount++;
            if (directionCount > MAX_ENTRIES_PER_DIRECTION) {
                entries.remove(index--);
            }
        }
        writeLocked(context, entries);
    }

    private static void writeLocked(Context context, List<Entry> entries) {
        JSONArray array = new JSONArray();
        for (int index = 0;
                index < entries.size() && index < MAX_ENTRIES;
                index++) {
            Entry item = entries.get(index);
            JSONObject value = new JSONObject();
            try {
                value.put("id", item.id);
                value.put("direction", item.direction);
                value.put("status", item.status);
                value.put("name", item.name);
                value.put("count", item.itemCount);
                value.put("folder", item.folder);
                value.put("time", item.timestamp);
                array.put(value);
            } catch (JSONException ignored) {
            }
        }
        SharedPreferences preferences = preferences(context);
        preferences.edit()
                .putString(KEY_ENTRIES, array.toString())
                .putLong(KEY_REVISION,
                        preferences.getLong(KEY_REVISION, 0L) + 1L)
                .apply();
    }

    private static List<Entry> readLocked(Context context) {
        List<Entry> result = new ArrayList<>();
        int phoneToPcCount = 0;
        int pcToPhoneCount = 0;
        String json = preferences(context).getString(KEY_ENTRIES, "[]");
        try {
            JSONArray array = new JSONArray(json == null ? "[]" : json);
            for (int index = 0;
                    index < array.length() && result.size() < MAX_ENTRIES;
                    index++) {
                JSONObject value = array.optJSONObject(index);
                if (value == null) {
                    continue;
                }
                String id = clean(value.optString("id", ""));
                if (id.isEmpty()) {
                    continue;
                }
                String direction = normalizeDirection(
                        value.optString("direction", ""));
                if (DIRECTION_PC_TO_PHONE.equals(direction)) {
                    if (pcToPhoneCount >= MAX_ENTRIES_PER_DIRECTION) {
                        continue;
                    }
                    pcToPhoneCount++;
                } else {
                    if (phoneToPcCount >= MAX_ENTRIES_PER_DIRECTION) {
                        continue;
                    }
                    phoneToPcCount++;
                }
                result.add(new Entry(id, direction,
                        value.optString("status", ""),
                        value.optString("name", ""),
                        value.optInt("count", 1),
                        value.optBoolean("folder", false),
                        value.optLong("time", 0L)));
            }
        } catch (JSONException ignored) {
        }
        return result;
    }

    private static Entry find(List<Entry> entries, String id) {
        String wanted = clean(id);
        for (Entry entry : entries) {
            if (entry.id.equals(wanted)) {
                return entry;
            }
        }
        return null;
    }

    private static SharedPreferences preferences(Context context) {
        return context.getSharedPreferences(PREFERENCES,
                Context.MODE_PRIVATE);
    }

    private static String rootName(String relativePath) {
        String value = clean(relativePath).replace('\\', '/');
        int separator = value.indexOf('/');
        return separator < 0 ? value : value.substring(0, separator);
    }

    private static String normalizeDirection(String value) {
        return DIRECTION_PC_TO_PHONE.equals(value)
                ? DIRECTION_PC_TO_PHONE : DIRECTION_PHONE_TO_PC;
    }

    private static String normalizeStatus(String value) {
        if (STATUS_TRANSFERRING.equals(value)
                || STATUS_COMPLETED.equals(value)
                || STATUS_FAILED.equals(value)
                || STATUS_CANCELED.equals(value)) {
            return value;
        }
        return STATUS_QUEUED;
    }

    private static String clean(String value) {
        return value == null ? "" : value.trim();
    }
}
