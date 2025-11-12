using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.Networking;         // pour UnityWebRequest

[Serializable]
public class VolumeLabelInfoRuntime
{
    public int labelIndex;        // 0..255
    public string displayName;    // "Tumeur", etc.
    public Color color;           // couleur TF initiale
    public bool defaultVisible;   // alpha > 0
}

[DisallowMultipleComponent]
public class VolumeDVR : MonoBehaviour
{
    [Header("Input (.vrdf in StreamingAssets)")]
    [Tooltip("Fichier fusionné anatomy_label_weighted (*_lw.vrdf). Si renseigné, on ignore les deux champs ci-dessous.")]
    public string vrdfFusedFileName = "scene_lw.vrdf";

    [Tooltip("Ancien mode: fichier labelmap (anatomy_label / labelmap TF). Ignoré si vrdfFusedFileName est renseigné.")]
    public string vrdfLabelsFileName = "scene_labels.vrdf";

    [Tooltip("Ancien mode: fichier weight (activity_weight / continuous TF). Peut être vide. Ignoré si vrdfFusedFileName est renseigné.")]
    public string vrdfWeightsFileName = "scene_weights.vrdfw";

    [Header("Raymarch material (must match shader)")]
    public Material volumeMaterial;

    [Header("Default VRDF file code")]
    public string defaultCode = "t1c";

    [Header("Debug / Quality")]
    [Tooltip("false = rendu clinique doux (soft LUT)\ntrue = rendu segmentation dure (hard LUT)")]
    public bool useHardTF = false;

    [Header("Runtime paths")]
    [Tooltip("Si vrai, on va chercher les .vrdf dans persistentDataPath d'abord (cache téléchargeable).")]
    public bool usePersistentDataFirst = true;

    public bool verboseDebug = false;

    [Range(0, 3)] public int debugMode = 0;

    private Texture3D volumeTexLabels;
    private Texture3D volumeTexWeights;

    private Texture2D tfTexCurrent;
    private Texture2D _labelCtrlTex;
    private Color[] _labelCtrlPixels;

    private Material _mat;

    Vector3 _lastScale = Vector3.zero;

    [NonSerialized] public List<VolumeLabelInfoRuntime> labelInfos = new List<VolumeLabelInfoRuntime>();

    private VRDFVolumeData _labelsData;

    private static Texture3D _blackTex3D;
    private static Texture3D BlackTex3D
    {
        get
        {
            if (_blackTex3D == null)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                _blackTex3D = new Texture3D(1, 1, 1, TextureFormat.R8, false);
                _blackTex3D.SetPixelData(new byte[] { 0 }, 0);
#else
                _blackTex3D = new Texture3D(1, 1, 1, TextureFormat.RFloat, false);
                _blackTex3D.SetPixel(0, 0, 0, new Color(0, 0, 0, 0));
#endif
                _blackTex3D.wrapMode = TextureWrapMode.Clamp;
                _blackTex3D.filterMode = FilterMode.Point;
                _blackTex3D.Apply(false, false);
            }
            return _blackTex3D;
        }
    }

    public DropdownVolumeLoader loader;
    

    void Awake()
    {
        // <-- LOGS AJOUTÉS -->
        Debug.LogWarning($"[VolumeDVR.Awake] -------------------------------------");
        Debug.Log($"[VolumeDVR.Awake] 1. Instanciation du matériau...");
        if (volumeMaterial == null)
        {
            Debug.LogError("[VolumeDVR.Awake] ERREUR: Le champ 'volumeMaterial' est vide (None) !");
            return;
        }

        var rend = GetComponent<Renderer>();
        SelectShaderForPlatform(volumeMaterial);
        Shader sh = volumeMaterial != null ? volumeMaterial.shader : Shader.Find("Volume/VolumeDVR_URP_Quest");
        _mat = new Material(sh) { name = "VolumeDVR_Runtime" };
        rend.material = _mat;
        volumeMaterial = _mat;

        Debug.Log($"[VolumeDVR.Awake] 2. Lecture des réglages DEPUIS: {volumeMaterial.name} (fichier .mat)");
        Debug.Log($"[VolumeDVR.Awake]    - Source LightIntensity: {volumeMaterial.GetFloat("_LightIntensity")}");
        Debug.Log($"[VolumeDVR.Awake]    - Source LightIntensity3 (Headlight): {volumeMaterial.GetFloat("_LightIntensity3")}");
        Debug.Log($"[VolumeDVR.Awake]    - Source Brightness: {volumeMaterial.GetFloat("_Brightness")}");
        Debug.Log($"[VolumeDVR.Awake]    - Source AlphaScale: {volumeMaterial.GetFloat("_AlphaScale")}");
        Debug.Log($"[VolumeDVR.Awake]    - Source StepCount: {volumeMaterial.GetFloat("_StepCount")}");

        Debug.Log($"[VolumeDVR.Awake] 3. Matériau instancié '{_mat.name}' avec succès.");
        Debug.LogWarning($"[VolumeDVR.Awake] -------------------------------------");
    }

    void LateUpdate()
    {
        var s = transform.lossyScale;
        if ((s - _lastScale).sqrMagnitude > 1e-6f)
        {
            float scaleMax = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            scaleMax = Mathf.Max(scaleMax, 0.001f);
            volumeMaterial.SetFloat("_ScaleCompensation", scaleMax);
            volumeMaterial.SetFloat("_ScaleMax", scaleMax);
            volumeMaterial.SetFloat("_DensityComp", 1.0f / Mathf.Max(1e-4f, scaleMax)); // 1/scale
            _lastScale = s;
        }
    }

    void OnDestroy()
    {
        ReleaseOldTextures();
        if (_mat) Destroy(_mat);
    }

    private void ReleaseOldTextures()
    {
        if (volumeTexLabels) { Destroy(volumeTexLabels); volumeTexLabels = null; }
        if (volumeTexWeights){ Destroy(volumeTexWeights); volumeTexWeights = null; }
        if (_labelCtrlTex)   { Destroy(_labelCtrlTex);   _labelCtrlTex = null; }
    }

    private void SelectShaderForPlatform(Material m)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string questName = "Volume/VolumeDVR_URP_Quest";
        var sh = Shader.Find(questName);
        if (sh == null) {
            Debug.LogError($"[VolumeDVR] Shader introuvable: {questName}. Ajoute-le à 'Always Included Shaders' ou référence-le dans un Material en Resources/");
            return;
        }
        m.shader = sh;
        Debug.Log($"[VolumeDVR] ✅ Shader Quest appliqué ({questName})");
#else
        const string deskName = "Custom/VolumeDVR_URP";
        var sh = Shader.Find(deskName);
        if (sh == null)
        {
            Debug.LogError($"[VolumeDVR] Shader introuvable: {deskName}. Ajoute-le à 'Always Included Shaders' ou référence-le dans un Material en Resources/");
            return;
        }
        m.shader = sh;
        Debug.Log($"[VolumeDVR] ✅ Shader desktop appliqué ({deskName})");
#endif
    }

    /// <summary>
    /// Version coroutine: tente d'abord le cache (persistentDataPath), sinon StreamingAssets (Android via UWR -> copie).
    /// Construit un nom de fichier strict: {code}_lw.vrdf
    /// </summary>
    // Dans VolumeDVR.cs
    public IEnumerator LoadVolumeByCodeAsync(string code)
    {
        Debug.LogWarning("[VolumeDVR.LoadVolume] -------------------------------------");
        Debug.Log($"[VolumeDVR.LoadVolume] DÉBUT: Demande de chargement du code '{code}'");

        if (string.IsNullOrEmpty(code))
        {
            Debug.LogError("[VolumeDVR] Code vide !");
            yield break;
        }

        string filename = $"{code.ToLowerInvariant()}_lw.vrdf";
        string cachePath = Path.Combine(Application.persistentDataPath, filename);
        string saPath = Path.Combine(Application.streamingAssetsPath, filename);

#if UNITY_ANDROID && !UNITY_EDITOR
    if (File.Exists(cachePath))
    {
        Debug.Log($"[VolumeDVR.LoadVolume] -> Cache (Android): {cachePath}");
        vrdfFusedFileName = filename;
        InternalLoadFused(cachePath);
        ApplyAfterLoad();
        Debug.LogWarning($"[VolumeDVR.LoadVolume] FIN: Chargement Cache '{code}' terminé.");
        yield break;
    }

    Debug.Log($"[VolumeDVR.LoadVolume] Cache introuvable, fallback scan (Android) pour '{code}'…");
    yield return LoadByScanningFallback(code);
    yield break;

#else
        if (File.Exists(cachePath))
        {
            Debug.Log($"[VolumeDVR.LoadVolume] -> Trouvé via CachePath: {cachePath}");
            vrdfFusedFileName = filename;
            InternalLoadFused(cachePath);
            ApplyAfterLoad();
            Debug.LogWarning($"[VolumeDVR.LoadVolume] FIN: Chargement CachePath '{code}' terminé.");
            yield break;
        }

        if (File.Exists(saPath))
        {
            Debug.Log($"[VolumeDVR.LoadVolume] -> Trouvé via StreamingAssets: {saPath}");
            vrdfFusedFileName = filename;
            InternalLoadFused(saPath);
            ApplyAfterLoad();
            Debug.LogWarning($"[VolumeDVR.LoadVolume] FIN: Chargement StreamingAssets '{code}' terminé.");
            yield break;
        }

        Debug.Log($"[VolumeDVR.LoadVolume] Fallback scan (Desktop/iOS) pour '{code}'…");
        yield return LoadByScanningFallback(code);
#endif
    }

    /// <summary>
    /// API synchrone conservée pour compat : démarre simplement la coroutine.
    /// </summary>
    public void LoadVolumeByCode(string code)
    {
        StartCoroutine(LoadVolumeByCodeAsync(code));
    }

    /// <summary>
    /// Fallback : reprend ta logique de scan (*.vrdf) dans persistentDataPath puis StreamingAssets.
    /// </summary>
    private IEnumerator LoadByScanningFallback(string code)
    {
        string lowerCode = code.ToLowerInvariant();
        string[] searchRoots;

#if UNITY_ANDROID && !UNITY_EDITOR
        searchRoots = new string[] { Application.persistentDataPath };
#else
        searchRoots = usePersistentDataFirst
            ? new string[] { Application.persistentDataPath, Application.streamingAssetsPath }
            : new string[] { Application.streamingAssetsPath, Application.persistentDataPath };
#endif

        string fileMatch = null;
        foreach (var root in searchRoots)
        {
            fileMatch = FindFileByCodeInRoot(root, code);
            if (fileMatch != null) break;
            yield return null; // laisse respirer la frame si gros dossier
        }

        if (fileMatch == null)
        {
            Debug.LogWarning($"[VolumeDVR] Aucun fichier trouvé contenant '{code}' en fallback (cache/StreamingAssets).");
            yield break;
        }

        Debug.Log($"[VolumeDVR] (fallback) Chargement : {fileMatch}");
        vrdfFusedFileName = Path.GetFileName(fileMatch);
        InternalLoadFused(fileMatch);
        ApplyAfterLoad();
    }

    private static bool FileMatchesCodeSuffix(string filePath, string code)
    {
        string name = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
        string c = code.ToLowerInvariant();

        // Exemples acceptés :
        // BraTS-GLI-00014-000-t1c_lw
        // t1c_lw
        // myPrefix-t2w_lw
        return name.EndsWith($"-{c}_lw") || name.EndsWith($"{c}_lw");
    }

    private string FindFileByCodeInRoot(string root, string code)
    {
        if (!Directory.Exists(root)) return null;

        string lowerCode = code.ToLowerInvariant();

        foreach (var f in Directory.GetFiles(root, "*.vrdf", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f).ToLowerInvariant().Contains(lowerCode))
            {
                return f;
            }
        }
        return null;
    }

    private void InternalLoadFused(string absolutePath)
    {
        CleanupPreviousTextures();
        if (string.IsNullOrEmpty(absolutePath))
        {
            Debug.LogError("[VolumeDVR] InternalLoadFused: chemin vide");
            return;
        }

        if (!File.Exists(absolutePath))
        {
            Debug.LogError($"[VolumeDVR] Fichier non trouvé : {absolutePath}");
            return;
        }

        _labelsData = VRDFLoader.LoadFromFile(absolutePath);
        VRDFLoader.BuildUnityTextures(_labelsData);

        volumeTexLabels = _labelsData.labelTexture;
        volumeTexWeights = _labelsData.weightTexture;

        tfTexCurrent = useHardTF ? _labelsData.tfLUTTextureHard
                                 : _labelsData.tfLUTTextureSoft;
    }
    private void ApplyAfterLoad()
    {
        ApplyToMaterial();
        InitLabelCtrlTexture();
        BuildLabelInfosFromData();
        FitVolumeScaleFromSpacing(_labelsData);

        if (verboseDebug)
            Debug.Log($"[VolumeDVR] Volume {vrdfFusedFileName} rechargé et appliqué.");
    }

    private string DimToString(int[] d)
    {
        if (d == null || d.Length < 3) return "???";
        return d[0] + "x" + d[1] + "x" + d[2];
    }

    public void SetTFMode(bool hard)
    {
        useHardTF = hard;
        tfTexCurrent = useHardTF ? _labelsData.tfLUTTextureHard
                                 : _labelsData.tfLUTTextureSoft;

        if (volumeMaterial != null && tfTexCurrent != null)
        {
            volumeMaterial.SetTexture("_TFTex", tfTexCurrent);
        }

        BuildLabelInfosFromData();
    }

    private void ApplyToMaterial()
    {
        var m = volumeMaterial;
        if (volumeMaterial == null)
        {
            Debug.LogError("[VolumeDVR] volumeMaterial not set.");
            return;
        }
        if (_labelsData == null || _labelsData.meta == null)
        {
            Debug.LogError("[VolumeDVR] Missing labels data.");
            return;
        }

        var d = _labelsData;

        int dimX = d.meta.dim[0];
        int dimY = d.meta.dim[1];
        int dimZ = d.meta.dim[2];


        float p1 = 0f;
        float p99 = 1f;
        if (d.tf.type == "continuous"
            && d.meta.intensity_range != null
            && d.meta.intensity_range.Length == 2)
        {
            p1 = d.meta.intensity_range[0];
            p99 = d.meta.intensity_range[1];
        }

        Matrix4x4 affine = Matrix4x4.identity;
        Matrix4x4 invAffine = Matrix4x4.identity;
        if (d.meta.affine != null)
        {
            affine.SetRow(0, new Vector4(d.meta.affine[0, 0], d.meta.affine[0, 1], d.meta.affine[0, 2], d.meta.affine[0, 3]));
            affine.SetRow(1, new Vector4(d.meta.affine[1, 0], d.meta.affine[1, 1], d.meta.affine[1, 2], d.meta.affine[1, 3]));
            affine.SetRow(2, new Vector4(d.meta.affine[2, 0], d.meta.affine[2, 1], d.meta.affine[2, 2], d.meta.affine[2, 3]));
            affine.SetRow(3, new Vector4(d.meta.affine[3, 0], d.meta.affine[3, 1], d.meta.affine[3, 2], d.meta.affine[3, 3]));
            invAffine = affine.inverse;
        }

        Texture3D texLbl = volumeTexLabels ?? d.labelTexture ?? BlackTex3D;
        Texture3D texW = volumeTexWeights ?? d.weightTexture ?? null;

        volumeMaterial.SetTexture("_VolumeTexLabels", texLbl ? texLbl : BlackTex3D);

        if (texW != null)
        {
            volumeMaterial.SetTexture("_VolumeTexWeights", texW);
            volumeMaterial.SetInt("_HasWeights", 1);
        }
        else
        {
            volumeMaterial.SetTexture("_VolumeTexWeights", BlackTex3D);
            volumeMaterial.SetInt("_HasWeights", 0);
        }

        m.SetTexture("_VolumeTexLabels", texLbl ? texLbl : BlackTex3D);
        m.SetTexture("_VolumeTexWeights", texW ? texW : BlackTex3D);
        m.SetInt("_HasWeights", texW ? 1 : 0);
        m.SetTexture("_TFTex", tfTexCurrent ? tfTexCurrent : Texture2D.blackTexture);
        m.SetInt("_IsLabelMap", (d.tf.type == "labelmap") ? 1 : 0);
        m.DisableKeyword("_DEBUG_MODE_LABELS");
        m.DisableKeyword("_DEBUG_MODE_WEIGHTS");
        m.DisableKeyword("_DEBUG_MODE_UVW");

        var mr = GetComponent<MeshRenderer>();
        if (mr && mr.sharedMaterial != volumeMaterial)
            mr.sharedMaterial = volumeMaterial;

        Debug.Log($"[VolumeDVR] Material push: labels={(texLbl != null ? texLbl.format.ToString() : "(null)")} weights={(texW != null ? texW.format.ToString() : "(null)")} hasW={volumeMaterial.GetInt("_HasWeights")}");
    }

    private void InitLabelCtrlTexture()
    {
        _labelCtrlTex = new Texture2D(256, 1, TextureFormat.RGBAFloat, false);
        _labelCtrlTex.wrapMode = TextureWrapMode.Clamp;
        _labelCtrlTex.filterMode = FilterMode.Point;
        _labelCtrlPixels = new Color[256];
        for (int i = 0; i < 256; i++)
            _labelCtrlPixels[i] = new Color(1f, 1f, 1f, 1f);
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);

        volumeMaterial.SetTexture("_LabelCtrlTex", _labelCtrlTex);
    }

    private void InitLabelCtrlTextureFromLUT()
    {
        if (tfTexCurrent == null) { InitLabelCtrlTexture_AllOn(); return; }

        var lut = tfTexCurrent.GetPixels();
        int len = Mathf.Min(256, lut.Length);

        _labelCtrlTex = new Texture2D(256, 1, TextureFormat.RGBA32, false, true);
        _labelCtrlTex.wrapMode = TextureWrapMode.Clamp;
        _labelCtrlTex.filterMode = FilterMode.Point;

        _labelCtrlPixels = new Color[256];
        for (int i = 0; i < 256; i++)
        {
            float a = (i < len) ? lut[i].a : 0f;   // ← alpha LUT
            Color c = (i < len) ? new Color(lut[i].r, lut[i].g, lut[i].b, a)
                                : new Color(1, 1, 1, 0); // par défaut masqué
            _labelCtrlPixels[i] = c;
        }
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false, true);
        volumeMaterial.SetTexture("_LabelCtrlTex", _labelCtrlTex);
    }

    private void InitLabelCtrlTexture_AllOn()
    {
        _labelCtrlTex = new Texture2D(256, 1, TextureFormat.RGBA32, false, true);
        _labelCtrlTex.wrapMode = TextureWrapMode.Clamp;
        _labelCtrlTex.filterMode = FilterMode.Point;
        _labelCtrlPixels = new Color[256];
        for (int i = 0; i < 256; i++) _labelCtrlPixels[i] = new Color(1,1,1,1);
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false, true);
        volumeMaterial.SetTexture("_LabelCtrlTex", _labelCtrlTex);
    }

    private void BuildLabelInfosFromData()
    {
        labelInfos.Clear();
        if (_labelsData == null || _labelsData.tf == null)
            return;

        Color[] lutPixels = null;
        int lutLen = 0;
        if (tfTexCurrent != null)
        {
            lutPixels = tfTexCurrent.GetPixels();
            lutLen = lutPixels.Length;
        }

        if (_labelsData.tf.entries != null && _labelsData.tf.entries.Count > 0)
        {
            foreach (var e in _labelsData.tf.entries)
            {
                int lbl = Mathf.RoundToInt(e.label);

                string niceName = !string.IsNullOrEmpty(e.name) ? e.name : ("Label " + lbl);

                Color baseCol = Color.clear;
                bool gotFromLut = false;

                if (lutPixels != null && lbl >= 0 && lbl < lutLen)
                {
                    baseCol = lutPixels[lbl];
                    gotFromLut = true;
                }

                if (!gotFromLut)
                {
                    float r = (e.color != null && e.color.Length > 0) ? e.color[0] : 1f;
                    float g = (e.color != null && e.color.Length > 1) ? e.color[1] : 1f;
                    float b = (e.color != null && e.color.Length > 2) ? e.color[2] : 1f;
                    float a = e.alpha;
                    baseCol = new Color(r, g, b, a);
                }

                bool visibleDefault = baseCol.a > 0.001f;

                var info = new VolumeLabelInfoRuntime
                {
                    labelIndex = lbl,
                    displayName = niceName,
                    color = baseCol,
                    defaultVisible = visibleDefault
                };
                labelInfos.Add(info);
            }
        }
        else if (lutPixels != null)
        {
            for (int lbl = 0; lbl < lutLen && lbl < 256; lbl++)
            {
                var c = lutPixels[lbl];
                bool visibleDefault = c.a > 0.001f;

                labelInfos.Add(new VolumeLabelInfoRuntime
                {
                    labelIndex = lbl,
                    displayName = "Label " + lbl,
                    color = c,
                    defaultVisible = visibleDefault
                });
            }
        }

        if (verboseDebug)
        {
            Debug.Log($"[VolumeDVR] Built labelInfos ({labelInfos.Count}) using {(useHardTF ? "HARD" : "SOFT")} LUT");
            foreach (var li in labelInfos)
            {
                Debug.Log($"  idx={li.labelIndex} name={li.displayName} col={li.color} visDefault={li.defaultVisible}");
            }
        }
    }

    private void FitVolumeScaleFromSpacing(VRDFVolumeData data)
    {
        var meta = data.meta;
        int dimX = meta.dim[0];
        int dimY = meta.dim[1];
        int dimZ = meta.dim[2];

        float sx = (meta.spacing_mm != null && meta.spacing_mm.Length > 0) ? meta.spacing_mm[0] : 1f;
        float sy = (meta.spacing_mm != null && meta.spacing_mm.Length > 1) ? meta.spacing_mm[1] : 1f;
        float sz = (meta.spacing_mm != null && meta.spacing_mm.Length > 2) ? meta.spacing_mm[2] : 1f;

        Vector3 sizeMeters = new Vector3(
            dimX * sx * 0.001f,
            dimY * sy * 0.001f,
            dimZ * sz * 0.001f
        );

        transform.localScale = sizeMeters;
        transform.localRotation = Quaternion.Euler(-90, 0, 0);
    }


    public void SetLabelVisible(int labelIndex, bool visible)
    {
        if (!EnsureCtrlReady()) return;
        if (labelIndex < 0 || labelIndex > 255) return;
        var c = _labelCtrlPixels[labelIndex];
        c.a = visible ? 1f : 0f;
        _labelCtrlPixels[labelIndex] = c;
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }

    public void SetLabelOpacity(int labelIndex, float opacity01)
    {
        if (!EnsureCtrlReady()) return;
        if (labelIndex < 0 || labelIndex > 255) return;
        var c = _labelCtrlPixels[labelIndex];
        c.a = Mathf.Clamp01(opacity01);
        _labelCtrlPixels[labelIndex] = c;
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }

    public void SetLabelTint(int labelIndex, Color tintRGB)
    {
        if (!EnsureCtrlReady()) return;
        if (labelIndex < 0 || labelIndex > 255) return;
        var c = _labelCtrlPixels[labelIndex];
        c.r = tintRGB.r;
        c.g = tintRGB.g;
        c.b = tintRGB.b;
        _labelCtrlPixels[labelIndex] = c;
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }

    public void SoloLabel(int soloIndex)
    {
        if (!EnsureCtrlReady()) return;
        for (int i = 0; i < 256; i++)
        {
            var c = _labelCtrlPixels[i];
            c.a = (i == soloIndex) ? 1f : 0f;
            _labelCtrlPixels[i] = c;
        }
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }

    public void ShowAll()
    {
        if (!EnsureCtrlReady()) return;
        for (int i = 0; i < 256; i++)
            _labelCtrlPixels[i] = new Color(1f, 1f, 1f, 1f);

        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }

    private bool EnsureCtrlReady()
    {
        if (_labelCtrlTex == null || _labelCtrlPixels == null)
        {
            Debug.LogWarning("[VolumeDVR] _labelCtrlTex not ready.");
            return false;
        }
        return true;
    }

    private void SafeDestroy(ref Texture3D tex)
    {
        if (tex != null) { Destroy(tex); tex = null; }
    }
    private void SafeDestroy(ref Texture2D tex)
    {
        if (tex != null) { Destroy(tex); tex = null; }
    }

    /// Appelé AVANT de charger un nouveau VRDF pour libérer l'ancien jeu de textures
    private void CleanupPreviousTextures()
    {
        // Débinde pour éviter les refs pendantes côté material
        if (volumeMaterial)
        {
            volumeMaterial.SetTexture("_VolumeTexLabels", BlackTex3D);
            volumeMaterial.SetTexture("_VolumeTexWeights", BlackTex3D);
            volumeMaterial.SetTexture("_TFTex", Texture2D.blackTexture);
            volumeMaterial.SetInt("_HasWeights", 0);
        }

        SafeDestroy(ref volumeTexLabels);
        SafeDestroy(ref volumeTexWeights);
        SafeDestroy(ref tfTexCurrent);

        // LabelCtrlTex est recréée à chaque load → détruis l’ancienne
        SafeDestroy(ref _labelCtrlTex);

        _labelCtrlPixels = null;

        // Optionnel: purge agressive (Android seulement)
    #if UNITY_ANDROID && !UNITY_EDITOR
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    #endif
    }
}
