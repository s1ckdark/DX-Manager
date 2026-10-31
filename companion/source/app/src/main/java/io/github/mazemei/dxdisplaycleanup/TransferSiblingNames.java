package io.github.mazemei.dxdisplaycleanup;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/** Allocates protocol path segments within one parent; original content URIs stay unchanged. */
final class TransferSiblingNames {
    private TransferSiblingNames() { }

    static String sanitize(String value) {
        String result = value == null ? "unnamed" : value.trim()
                .replace('/', '_').replace('\\', '_');
        return result.isEmpty() || ".".equals(result) || "..".equals(result)
                ? "unnamed" : result;
    }

    static List<String> allocate(List<String> names, List<Boolean> directories) {
        if (names.size() != directories.size()) {
            throw new IllegalArgumentException("Each name must have a directory flag.");
        }
        List<String> bases = new ArrayList<>();
        Set<String> reserved = new HashSet<>();
        for (String name : names) {
            String base = sanitize(name);
            bases.add(base);
            reserved.add(base);
        }
        Set<String> used = new HashSet<>();
        List<String> result = new ArrayList<>();
        for (int index = 0; index < bases.size(); index++) {
            String base = bases.get(index);
            String candidate = base;
            int suffix = 1;
            while (used.contains(candidate)) {
                int dot = directories.get(index) ? -1 : base.lastIndexOf('.');
                String stem = dot > 0 ? base.substring(0, dot) : base;
                String extension = dot > 0 ? base.substring(dot) : "";
                do {
                    candidate = stem + " (" + suffix++ + ")" + extension;
                } while (reserved.contains(candidate));
            }
            used.add(candidate);
            result.add(candidate);
        }
        return result;
    }
}
