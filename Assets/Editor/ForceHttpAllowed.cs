using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class ForceHttpAllowed : IPreprocessBuildWithReport {
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) {
        // Unity 2021+ : force l’option projet
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        // Android: s'assure aussi que l'Internet est requis
        PlayerSettings.Android.forceInternetPermission = true;
        AssetDatabase.SaveAssets();
    }
}
