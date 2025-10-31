using UnityEngine;

public class VRPinAndToggle : MonoBehaviour
{
    public StudyInfoPanel panel;

    public void ToggleFollow()
    {
        if (panel == null) return;
        panel.followHead = !panel.followHead;
    }

    public void ToggleConfidentialRedact()
    {
        if (panel == null) return;
        bool next = !panel.confidentialMode;
        panel.SetConfidentialMode(next, StudyInfoPanel.ConfidentialBehavior.Redact);
    }

    public void ToggleConfidentialHide()
    {
        if (panel == null) return;
        bool next = !panel.confidentialMode;
        panel.SetConfidentialMode(next, StudyInfoPanel.ConfidentialBehavior.Hide);
    }
}
