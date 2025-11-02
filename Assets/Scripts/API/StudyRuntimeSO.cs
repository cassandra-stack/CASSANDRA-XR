using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StudyRuntime", menuName = "XR/Study Runtime")]
public class StudyRuntimeSO : ScriptableObject
{
    [Header("Study (runtime)")]
    public int id;
    public string title;
    public string code;
    public string studyDate;
    public bool isVr;
    public string converationURL;

    [Header("Patient (runtime)")]
    public PatientInfo patient = new PatientInfo();

    [Header("Assets (runtime)")]
    public List<VrdfAsset> vrdfAssets = new List<VrdfAsset>();

    [Header("Meta")]
    public bool hasData;
    public long lastUpdatedEpoch;

    [field: NonSerialized] public event Action OnChanged;

    /// <summary>Efface totalement l'état courant (utilisé avant un nouveau chargement).</summary>
    public void Clear()
    {
        id = 0;
        title = string.Empty;
        code = string.Empty;
        studyDate = string.Empty;
        isVr = false;
        converationURL = string.Empty;

        if (patient == null) patient = new PatientInfo();
        patient.id = 0;
        patient.firstName = string.Empty;
        patient.lastName = string.Empty;
        patient.dateOfBirth = string.Empty;
        patient.gender = string.Empty;

        vrdfAssets ??= new List<VrdfAsset>();
        vrdfAssets.Clear();

        hasData = false;
        lastUpdatedEpoch = NowEpoch();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Remplace l’état par une nouvelle étude. Fait des copies défensives des listes/objets.
    /// </summary>
    public void Apply(StudyForUnity study)
    {
        if (study == null)
        {
            Clear();
            return;
        }

        id = study.id;
        title = study.title ?? string.Empty;
        code = study.code ?? string.Empty;
        studyDate = study.studyDate ?? string.Empty;
        isVr = study.isVr;
        converationURL = study.converationURL ?? string.Empty;

        if (patient == null) patient = new PatientInfo();
        if (study.patient != null)
        {
            patient.id = study.patient.id;
            patient.firstName = study.patient.firstName ?? string.Empty;
            patient.lastName = study.patient.lastName ?? string.Empty;
            patient.dateOfBirth = study.patient.dateOfBirth ?? string.Empty;
            patient.gender = study.patient.gender ?? string.Empty;
        }
        else
        {
            patient.id = 0;
            patient.firstName = string.Empty;
            patient.lastName = string.Empty;
            patient.dateOfBirth = string.Empty;
            patient.gender = string.Empty;
        }

        vrdfAssets = new List<VrdfAsset>();
        if (study.vrdfAssets != null)
        {
            foreach (var a in study.vrdfAssets)
            {
                if (a == null) continue;
                vrdfAssets.Add(new VrdfAsset
                {
                    filename = a.filename ?? string.Empty,
                    modality = a.modality ?? string.Empty,
                    downloadUrl = a.downloadUrl ?? string.Empty
                });
            }
        }

        hasData = true;
        lastUpdatedEpoch = NowEpoch();
        OnChanged?.Invoke();
    }

    // ------------------------------
    // Helpers pratiques (lecture)
    // ------------------------------

    /// <summary>Nom complet patient "Prénom NOM". Renvoie string.Empty si pas de données.</summary>
    public string PatientFullName()
    {
        if (patient == null) return string.Empty;
        var first = patient.firstName ?? string.Empty;
        var last  = patient.lastName  ?? string.Empty;
        if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(last)) return string.Empty;
        return string.IsNullOrEmpty(last) ? first : (string.IsNullOrEmpty(first) ? last : $"{first} {last}".Trim());
    }

    /// <summary>Âge du patient (années, approximation) si date au format ISO (yyyy-MM-dd) ou assimilée. -1 si inconnu.</summary>
    public int PatientAgeYears()
    {
        if (patient == null || string.IsNullOrWhiteSpace(patient.dateOfBirth)) return -1;
        if (!TryParseDate(patient.dateOfBirth, out var dob)) return -1;

        var today = DateTime.UtcNow.Date;
        int age = today.Year - dob.Year;
        if (dob.Date > today.AddYears(-age)) age--;
        return Mathf.Max(0, age);
    }

    /// <summary>Retourne la première asset pour une modalité donnée (insensible à la casse). Null si absente.</summary>
    public VrdfAsset GetAssetByModality(string modality)
    {
        if (vrdfAssets == null || vrdfAssets.Count == 0 || string.IsNullOrEmpty(modality)) return null;
        var m = modality.Trim().ToLowerInvariant();
        return vrdfAssets.Find(a => (a?.modality ?? "").Trim().ToLowerInvariant() == m);
    }

    /// <summary>Liste distincte des modalités disponibles.</summary>
    public List<string> GetModalities()
    {
        var set = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        if (vrdfAssets != null)
        {
            foreach (var a in vrdfAssets)
            {
                if (!string.IsNullOrWhiteSpace(a?.modality))
                    set.Add(a.modality);
            }
        }
        return new List<string>(set);
    }

    /// <summary>Essaie de parser studyDate. Retourne false si invalide.</summary>
    public bool TryGetStudyDate(out DateTime dateUtc)
    {
        dateUtc = default;
        if (string.IsNullOrWhiteSpace(studyDate)) return false;
        return TryParseDate(studyDate, out dateUtc);
    }

    // ------------------------------
    // Internes
    // ------------------------------

    private static bool TryParseDate(string s, out DateTime dt)
    {
        var formats = new[]
        {
            "yyyy-MM-dd","yyyy/MM/dd","dd/MM/yyyy","MM/dd/yyyy",
            "yyyy-MM-ddTHH:mm:ssZ","yyyy-MM-ddTHH:mm:ss","yyyy-MM-ddTHH:mm:ss.fffZ"
        };
        if (DateTime.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture,
                                   System.Globalization.DateTimeStyles.AssumeUniversal |
                                   System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            dt = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }
        // fallback TryParse général
        if (DateTime.TryParse(s, out parsed))
        {
            dt = DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc);
            return true;
        }
        dt = default;
        return false;
    }

    private static long NowEpoch()
    {
        return (long)(DateTime.UtcNow - new DateTime(1970,1,1)).TotalSeconds;
    }
}
