using UnityEngine;
using TMPro;
using System;
using System.Text;

public class StudyInfoPanel : MonoBehaviour
{
    public enum ConfidentialBehavior { Redact, Hide }

    public TextMeshProUGUI titleStudy, codeStudy, dateStudy, modeStudy;
    public TextMeshProUGUI namePatient, dobPatient, genderPatient;
    public TextMeshProUGUI urlLabel, urlValue;
    public UnityEngine.UI.Button btnCopy, btnOpen;
    public GameObject badgeConfidential;

    [Header("Bindings")]
    public StudyRuntimeSO studyState;        // ton SO existant
    public TextMeshProUGUI infoText;         // texte principal (ou laisse vide si tu bind champ par champ)
    public CanvasGroup canvasGroup;          // pour hide/fade proprement

    [Header("Confidential Mode")]
    public bool confidentialMode = false;
    public ConfidentialBehavior behavior = ConfidentialBehavior.Redact;

    [Tooltip("Affiche le panneau accroché (head-locked) si vrai, sinon flottant dans l'espace.")]
    public bool followHead = true;
    public Transform head;                   // Camera (HMD) transform
    public float followDistance = 1.2f;
    public Vector3 followOffset = new Vector3(0, -0.05f, 0);

    [Header("Formatting")]
    public bool english = true;              // UI en anglais (true) ou FR (false)
    public bool showPatientGender = true;
    public bool showStudyDate = true;

    void OnEnable()
    {
        if (studyState != null) studyState.OnChanged += HandleStudyChanged;
        Refresh();
    }

    void OnDisable()
    {
        if (studyState != null) studyState.OnChanged -= HandleStudyChanged;
    }

    void Update()
    {
        if (followHead && head != null) FollowHeadPose();
    }

    private void HandleStudyChanged() => Refresh();

    public void SetConfidentialMode(bool on, ConfidentialBehavior newBehavior)
    {
        confidentialMode = on;
        behavior = newBehavior;
        Refresh();
    }

    public void Refresh()
    {
        if (studyState == null) { SetVisible(false); return; }

        if (confidentialMode && behavior == ConfidentialBehavior.Hide)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        // Récupération brute
        var title = studyState.title;
        var code  = studyState.code;
        var date  = studyState.studyDate;
        var isVr  = studyState.isVr;

        var p = studyState.patient;
        string first = p?.firstName;
        string last  = p?.lastName;
        string dob   = p?.dateOfBirth;
        string gender= p?.gender;

        // Redaction si nécessaire
        if (confidentialMode && behavior == ConfidentialBehavior.Redact)
        {
            title = RedactStudy(title, code);
            code  = null; // on n’affiche pas le code en plus
            (first, last, dob) = RedactPatient(first, last, dob);
            gender = null; // optionnel: on peut supprimer le genre
        }

        // Build texte
        if (infoText != null)
            infoText.text = english ? BuildEnglish(title, code, date, isVr, first, last, dob, gender)
                                    : BuildFrench (title, code, date, isVr, first, last, dob, gender);
    }

    private string BuildEnglish(string title, string code, string date, bool isVr, string first, string last, string dob, string gender)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>Study</b>: {Or(title, "Unspecified")}");
        if (!string.IsNullOrWhiteSpace(code)) sb.AppendLine($"<b>Code</b>: {code}");
        if (showStudyDate) sb.AppendLine($"<b>Date</b>: {Or(date, "—")}");
        sb.AppendLine($"<b>XR Mode</b>: {(isVr ? "VR" : "Desktop")}");

        sb.AppendLine();
        sb.AppendLine("<b>Patient</b>");
        sb.AppendLine($"• Name: {Or(FullName(first, last), "Unspecified")}");
        if (!string.IsNullOrWhiteSpace(dob)) sb.AppendLine($"• DOB: {dob}");
        if (showPatientGender && !string.IsNullOrWhiteSpace(gender)) sb.AppendLine($"• Gender: {gender}");

        // Optionnel: URL de conversation (attention PHI)
        if (!confidentialMode && !string.IsNullOrWhiteSpace(studyState.converationURL))
        {
            sb.AppendLine();
            sb.AppendLine($"<i>Conversation:</i> {studyState.converationURL}");
        }
        return sb.ToString();
    }

    private string BuildFrench(string title, string code, string date, bool isVr, string first, string last, string dob, string gender)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>Étude</b> : {Or(title, "Non renseignée")}");
        if (!string.IsNullOrWhiteSpace(code)) sb.AppendLine($"<b>Code</b> : {code}");
        if (showStudyDate) sb.AppendLine($"<b>Date</b> : {Or(date, "—")}");
        sb.AppendLine($"<b>Mode XR</b> : {(isVr ? "VR" : "Desktop")}");

        sb.AppendLine();
        sb.AppendLine("<b>Patient</b>");
        sb.AppendLine($"• Nom : {Or(FullName(first, last), "Non renseigné")}");
        if (!string.IsNullOrWhiteSpace(dob)) sb.AppendLine($"• Date de naissance : {dob}");
        if (showPatientGender && !string.IsNullOrWhiteSpace(gender)) sb.AppendLine($"• Genre : {gender}");

        if (!confidentialMode && !string.IsNullOrWhiteSpace(studyState.converationURL))
        {
            sb.AppendLine();
            sb.AppendLine($"<i>Conversation :</i> {studyState.converationURL}");
        }
        return sb.ToString();
    }

    private string Or(string v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v;
    private string FullName(string first, string last)
    {
        if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)) return null;
        first = first?.Trim() ?? "";
        last  = last?.Trim() ?? "";
        var space = (first.Length > 0 && last.Length > 0) ? " " : "";
        return first + space + last;
    }

    // --- Redaction helpers ---
    private string RedactStudy(string title, string code)
    {
        // exemple simple: on retire tout identifiant potentiel
        if (!string.IsNullOrWhiteSpace(title)) return "Study (redacted)";
        if (!string.IsNullOrWhiteSpace(code))  return "Study (redacted)";
        return "Study";
    }

    private (string first, string last, string dob) RedactPatient(string first, string last, string dob)
    {
        // Nom -> Initiales
        string firstRed = string.IsNullOrWhiteSpace(first) ? "" : first.Trim();
        string lastRed  = string.IsNullOrWhiteSpace(last)  ? "" : last.Trim();

        string initials;
        if (firstRed.Length > 0 && lastRed.Length > 0) initials = $"{char.ToUpper(firstRed[0])}. {char.ToUpper(lastRed[0])}.";
        else if (lastRed.Length > 0) initials = $"{char.ToUpper(lastRed[0])}.";
        else if (firstRed.Length > 0) initials = $"{char.ToUpper(firstRed[0])}.";
        else initials = "Patient";

        // DOB -> Année seulement si parsable, sinon rien
        string dobOut = null;
        if (!string.IsNullOrWhiteSpace(dob) && DateTime.TryParse(dob, out var dt)) dobOut = dt.Year.ToString();

        return (initials, "", dobOut);
    }

    private void SetVisible(bool vis)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = vis ? 1f : 0f;
            canvasGroup.interactable = vis;
            canvasGroup.blocksRaycasts = vis;
        }
        else
        {
            gameObject.SetActive(vis);
        }
    }

    private void FollowHeadPose()
    {
        // positionne le panneau devant le HMD, légèrement en dessous du regard
        var targetPos = head.position + head.forward * followDistance + head.TransformVector(followOffset);
        transform.position = Vector3.Lerp(transform.position, targetPos, 0.25f);

        // oriente vers la tête (pas de roll)
        Vector3 forward = (transform.position - head.position);
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward.normalized, Vector3.up), 0.25f);
    }
}
