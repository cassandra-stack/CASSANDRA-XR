using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CassandraHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
  public Image glow; public Color color;
  void Awake(){ if (glow){ glow.color = new Color(color.r, color.g, color.b, 0f); glow.raycastTarget=false; } }
  public void OnPointerEnter(PointerEventData e){ if (glow) glow.CrossFadeAlpha(1f, 0.12f, true); }
  public void OnPointerExit (PointerEventData e){ if (glow) glow.CrossFadeAlpha(0f, 0.15f, true); }
}
