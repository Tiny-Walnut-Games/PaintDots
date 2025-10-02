using UnityEditor;
using PaintDots.Editor.ABCs;

public class AutoBioChromaWindow : EditorWindow
{
    [MenuItem("PaintDots/AutoBioChroma Slider")]
    public static void ShowWindow()
    {
        GetWindow<AutoBioChromaWindow>("ABCs");
    }

    private void OnGUI()
    {
        AutoBioChromaModule.DrawStandalone();
    }
}
