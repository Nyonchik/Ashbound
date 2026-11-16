using UnityEditor;

/// <summary>
/// Runs Ashbound > Build Everything automatically when the project opens and the
/// Game scene doesn't exist yet. After that, use the Ashbound menu to rebuild.
/// </summary>
[InitializeOnLoad]
static class AshboundAutoBuild
{
    const string GameScene = "Assets/_Ashbound/Scenes/Game.unity";
    static int waits;

    static AshboundAutoBuild()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) != null) return;
        EditorApplication.delayCall += TryBuild;
    }

    static void TryBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if ((EditorApplication.isCompiling || EditorApplication.isUpdating) && waits++ < 600)
        {
            EditorApplication.delayCall += TryBuild; // wait until the editor is idle
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) == null)
            AshboundBuilder.BuildEverything();
    }
}
