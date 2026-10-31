package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;
import java.util.Arrays;
import java.util.List;
import static org.junit.Assert.assertEquals;

public class TransferSiblingNamesTest {
    private List<String> files(String... names) {
        Boolean[] directories = new Boolean[names.length];
        Arrays.fill(directories, false);
        return TransferSiblingNames.allocate(Arrays.asList(names), Arrays.asList(directories));
    }
    @Test public void leadingAndTrailingSpaceCollisionsRemainDistinct() {
        assertEquals(Arrays.asList("photo.txt", "photo (1).txt", "photo (2).txt"),
                files("photo.txt", " photo.txt", "photo.txt "));
    }
    @Test public void slashReplacementDoesNotMergeSiblings() {
        assertEquals(Arrays.asList("A_B", "A_B (1)", "A_B (2)"), files("A/B", "A_B", "A\\B"));
    }
    @Test public void generatedNamesDoNotStealExistingSuffixNames() {
        assertEquals(Arrays.asList("file.txt", "file (3).txt", "file (1).txt", "file (2).txt"),
                files("file.txt", " file.txt", "file (1).txt", "file (2).txt"));
    }
    @Test public void filesAndFoldersShareTheirParentNamespace() {
        assertEquals(Arrays.asList("folder.ext", "folder.ext (2)", "folder.ext (1)", "folder (1).ext"),
                TransferSiblingNames.allocate(Arrays.asList("folder.ext", " folder.ext", "folder.ext (1)", "folder.ext "),
                        Arrays.asList(true, true, true, false)));
    }
    @Test public void emptyDotAndHiddenNamesCannotMerge() {
        assertEquals(Arrays.asList("unnamed", "unnamed (1)", "unnamed (2)", ".hidden", ".hidden (1)"),
                files(null, " . ", "..", ".hidden", " .hidden "));
    }
    @Test public void ordinaryUnicodeAndSpacesBeforeExtensionStayUnchanged() {
        assertEquals(Arrays.asList("사진  .txt", "日本語.png", "a b.txt", "emoji-\uD83D\uDCF7.png"),
                files("사진  .txt", "日本語.png", "a b.txt", "emoji-\uD83D\uDCF7.png"));
    }
    @Test public void separateParentsCanUseTheSameName() {
        assertEquals(files("file.txt", " file.txt"), files("file.txt", " file.txt"));
        assertEquals(Arrays.asList("file.txt"), files("file.txt"));
    }
}
