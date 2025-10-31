using UnityEngine;

[CreateAssetMenu(menuName="Cassandra/Theme")]
public class CassandraTheme : ScriptableObject {
  [Header("Couleurs")]
  public Color colBackground = new Color32(0xF4,0xF6,0xF8,0xFF); // fond très clair
  public Color colCard = Color.white;                             // cartes
  public Color colGlass = new Color(1,1,1,0.7f);                   // header translucide
  public Color colTextStrong = new Color32(0x2F,0x3B,0x45,0xFF);   // anthracite
  public Color colTextSubtle = new Color(0.18f,0.23f,0.27f,0.85f); // anthracite 85%
  public Color colAccent = new Color32(0x4A,0x90,0xE2,0xFF);       // bleu doux
  public Color colGlow = new Color32(0x6F,0xC3,0xD0,0x55);         // halo discret
}
