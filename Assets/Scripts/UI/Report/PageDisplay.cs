using UnityEngine;

public class PageDisplay : MonoBehaviour {
    public Texture2D[] pages;
    public int currentPage = 0;
    public Renderer displayRenderer;

    void Start() {
        ShowPage(0);
    }

    public void ShowPage(int index) {
        if (index >= 0 && index < pages.Length) {
            currentPage = index;
            displayRenderer.material.mainTexture = pages[index];
        }
    }

    public void NextPage() => ShowPage(currentPage + 1);
    public void PrevPage() => ShowPage(currentPage - 1);
}
