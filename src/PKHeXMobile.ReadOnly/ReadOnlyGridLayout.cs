namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Pure layout policy for a read-only slot grid. Keep slot order stable while
/// reducing columns for narrow screens or large Android font settings.
/// This is a display heuristic, not an accessibility/device certification.
/// </summary>
public static class ReadOnlyGridLayout
{
    public static int ColumnsForWidth(int availableWidthDp, float fontScale)
    {
        if (availableWidthDp <= 0)
            return 1;
        if (!float.IsFinite(fontScale) || fontScale <= 0)
            fontScale = 1;
        // 64 dp baseline per slot; up to 2.5x for large accessibility text.
        int minWidth = (int)Math.Ceiling(64 * Math.Clamp(fontScale, 1f, 2.5f));
        return Math.Clamp(availableWidthDp / minWidth, 1, 6);
    }
}
