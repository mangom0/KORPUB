using UnityEngine;

public class CharacterPreviewInScene : MonoBehaviour
{
    [SerializeField] private GameObject[] models; // 8개 캐릭터 프리뷰 오브젝트
    private int index = 0;

    private void Start()
    {
        Apply();
        SetVisible(false);
    }

    public void SetVisible(bool v)
    {
        gameObject.SetActive(v);
        if (v) Apply();
    }

    public int GetIndex() => index;

    public void Next()
    {
        if (models == null || models.Length == 0) return;
        index = (index + 1) % models.Length;
        Apply();
    }

    public void Prev()
    {
        if (models == null || models.Length == 0) return;
        index = (index - 1 + models.Length) % models.Length;
        Apply();
    }

    private void Apply()
    {
        if (models == null) return;
        for (int i = 0; i < models.Length; i++)
        {
            if (models[i] != null)
                models[i].SetActive(i == index);
        }
    }
}
