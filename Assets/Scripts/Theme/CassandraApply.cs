// CassandraApply.cs (patch)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class CassandraApply : MonoBehaviour {
  public CassandraTheme theme;
  public TMP_FontAsset sansSerif;
  public TMP_FontAsset mono;

  // IMPORTANT : attendre que l'UI soit attachée au panel/canvas
  private System.Collections.IEnumerator Start(){
    // Attendre 1 frame (ou 2 si nécessaire)
    yield return null;

    // Si la scène charge encore, on peut attendre la fin de frame
    // yield return new WaitForEndOfFrame();

    SafeApply();
  }

  [ContextMenu("Apply Cassandra Style")]
  public void Apply() => SafeApply();

  private void SafeApply(){
    if (!isActiveAndEnabled || theme == null) return;

    // — Fond
    var bg = transform.Find("Panel.Root")?.GetComponent<Image>();
    if (bg) bg.color = theme.colBackground;

    // — Header
    var bar = transform.Find("Panel.Root/Header.Bar")?.GetComponent<Image>();
    if (bar) bar.color = theme.colGlass;

    var title = transform.Find("Panel.Root/Header.Bar/Title")?.GetComponent<TMP_Text>();
    if (title){
      title.color = theme.colTextStrong;
      if (sansSerif) title.font = sansSerif;
      title.fontSize = 50;
      title.fontStyle = FontStyles.Bold;
    }

    // — Cartes
    string[] cards = { "Panel.Root/Body/Card.Study", "Panel.Root/Body/Card.Patient" };
    foreach (var p in cards){
      var img = transform.Find(p)?.GetComponent<Image>();
      if (img) img.color = theme.colCard;

      var cardTitlePath = p + (p.Contains("Study")?"/Study":"/Patient");
      var cardTitle = transform.Find(cardTitlePath)?.GetComponent<TMP_Text>();
      if (cardTitle){
        cardTitle.color = theme.colTextStrong;
        if (sansSerif) cardTitle.font = sansSerif;
        cardTitle.fontSize = 34;
        cardTitle.fontStyle = FontStyles.Bold;
      }
    }

    // — Labels & valeurs
    foreach (var tmp in GetComponentsInChildren<TMP_Text>(true)) {
      if (tmp == null) continue;
      if (tmp.name.StartsWith("Value.")) {
        tmp.color = theme.colTextStrong;
        if (mono) tmp.font = mono;
        tmp.fontSize = 24;
        tmp.fontStyle = FontStyles.Normal;
      } else if (IsLabel(tmp.name)) {
        tmp.color = theme.colTextSubtle;
        if (sansSerif) tmp.font = sansSerif;
        tmp.fontSize = 24;
        tmp.fontStyle = FontStyles.Normal;
      }
    }
  }

  bool IsLabel(string n){
    string[] labels = { "Title","Code","Date","Name","DOB","Gender","Study","Patient" };
    return labels.Contains(n);
  }
}
