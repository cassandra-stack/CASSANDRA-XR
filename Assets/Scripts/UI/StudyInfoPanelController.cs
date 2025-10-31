using System;
using System.Globalization;
using TMPro;
using UnityEngine;

public class StudyInfoPanelController : MonoBehaviour
{
    public enum ConfidentialBehavior { Redact, Hide }
    public enum DateStyle { Full, Short }

    [Header("Data Source")]
    public StudyRuntimeSO studyState;

    [Header("Language")]
    public bool english = true;

    [Header("Date Format")]
    public DateStyle dateStyle = DateStyle.Full;   // <-- Choix dans l’Inspector

    [Header("Confidential Mode")]
    public bool confidentialMode = false;
    public ConfidentialBehavior behavior = ConfidentialBehavior.Redact;

    [Header("Root Visibility")]
    public CanvasGroup canvasGroup;

    [Header("Study Card")]
    public TextMeshProUGUI hStudyTitle;
    public TextMeshProUGUI valueStudyTitle;
    public TextMeshProUGUI valueStudyCode;
    public TextMeshProUGUI valueStudyDate;
    public TextMeshProUGUI valueStudyMode;

    [Header("Patient Card")]
    public TextMeshProUGUI hPatientTitle;
    public TextMeshProUGUI valuePatientName;
    public TextMeshProUGUI valuePatientDOB;
    public TextMeshProUGUI valuePatientGender;

    [Header("Optional Badge")]
    public GameObject badgeConfidential;

    private void OnEnable()
    {
        if (studyState != null) studyState.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (studyState != null) studyState.OnChanged -= Refresh;
    }

    public void SetConfidentialMode(bool on, ConfidentialBehavior newBehavior)
    {
        confidentialMode = on;
        behavior = newBehavior;
        Refresh();
    }

    public void Refresh()
    {
        if (studyState == null) { SetVisible(false); return; }
        if (confidentialMode && behavior == ConfidentialBehavior.Hide) { SetVisible(false); return; }
        SetVisible(true);

        // --- Read
        string studyTitle = studyState.title;
        string studyCode  = studyState.code;
        string studyDate  = studyState.studyDate;
        bool   isVr       = studyState.isVr;

        string first  = studyState.patient?.firstName;
        string last   = studyState.patient?.lastName;
        string dob    = studyState.patient?.dateOfBirth;
        string gender = studyState.patient?.gender;

        // --- Confidential redaction
        if (confidentialMode && behavior == ConfidentialBehavior.Redact)
        {
            studyTitle = english ? "Study (redacted)" : "Étude (masquée)";
            studyCode  = null;
            (first, last, dob) = RedactPatient(first, last, dob);
        }

        // --- Write
        if (hStudyTitle)   hStudyTitle.text   = english ? "Study"   : "Étude";
        if (hPatientTitle) hPatientTitle.text = english ? "Patient" : "Patient";

        if (valueStudyTitle) valueStudyTitle.text = Or(studyTitle, english ? "Unspecified" : "Non renseignée");
        if (valueStudyCode)  valueStudyCode.text  = Or(studyCode,  "—");

        if (valueStudyDate)  valueStudyDate.text  = PrettyDate(studyDate, english, dateStyle);
        if (valueStudyMode)  valueStudyMode.text  = isVr ? "VR" : (english ? "Desktop" : "Bureau");

        if (valuePatientName)   valuePatientName.text   = Or(FullName(first, last), english ? "Unspecified" : "Non renseigné");
        if (valuePatientDOB)    valuePatientDOB.text    = PrettyDate(dob, english, dateStyle);
        if (valuePatientGender) valuePatientGender.text = Or(gender, "—");

        if (badgeConfidential)  badgeConfidential.SetActive(confidentialMode);
    }

    // ---------- Helpers ----------
    private void SetVisible(bool vis)
    {
        if (!canvasGroup) { gameObject.SetActive(vis); return; }
        canvasGroup.alpha = vis ? 1f : 0f;
        canvasGroup.interactable = vis;
        canvasGroup.blocksRaycasts = vis;
    }

    private static string Or(string v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v;

    private static string FullName(string first, string last)
    {
        if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)) return null;
        first = first?.Trim() ?? "";
        last  = last?.Trim()  ?? "";
        return (first + " " + last).Trim();
    }

    private (string first, string last, string dob) RedactPatient(string first, string last, string dob)
    {
        string initials = "Patient";
        string f = first?.Trim() ?? "";
        string l = last?.Trim()  ?? "";

        if (!string.IsNullOrEmpty(f) && !string.IsNullOrEmpty(l))      initials = $"{char.ToUpper(f[0])}. {char.ToUpper(l[0])}.";
        else if (!string.IsNullOrEmpty(l))                              initials = $"{char.ToUpper(l[0])}.";
        else if (!string.IsNullOrEmpty(f))                              initials = $"{char.ToUpper(f[0])}.";

        string dobOut = null;
        if (!string.IsNullOrWhiteSpace(dob) && DateTime.TryParse(dob, out var dt))
            dobOut = dt.Year.ToString();

        return (initials, "", dobOut);
    }

    /// <summary>
    /// Date lisible (EN/FR) – Full: “October 29, 2025” / “29 octobre 2025”
    /// Short: “Oct 29, 2025” / “29 oct. 2025”.
    /// Accepte ISO, formats communs, ou juste une année ("1940").
    /// </summary>
    private string PrettyDate(string raw, bool en, DateStyle style)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "—";

        // Cas "année seule" (ex. redaction)
        if (raw.Length == 4 && int.TryParse(raw, out _)) return raw;

        // Listes de formats probables
        string[] formats = {
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy"
        };

        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
            || DateTime.TryParse(raw, out dt))
        {
            var culture = en ? new CultureInfo("en-US") : new CultureInfo("fr-FR");

            if (style == DateStyle.Full)
            {
                // October 29, 2025  |  29 octobre 2025
                string fmt = en ? "MMMM d, yyyy" : "d MMMM yyyy";
                var s = dt.ToString(fmt, culture);
                return CapitalizeFirst(s);
            }
            else
            {
                // Oct 29, 2025  |  29 oct. 2025
                // (abréviation locale courte)
                string month = dt.ToString(en ? "MMM" : "MMM", culture);
                if (!en) month = month.ToLower() + ".";   // “oct.”, “nov.”, etc.
                return en
                    ? $"{month} {dt.Day}, {dt.Year}"
                    : $"{dt.Day} {month} {dt.Year}";
            }
        }

        // Fallback : nettoie artefacts ISO
        return raw.Replace("T00:00:00.0000000Z", "")
                  .Replace("T00:00:00Z", "")
                  .Replace("T00:00:00", "")
                  .Trim();
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }
}
