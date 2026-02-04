using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class CharacterModelSelector : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject[] models; // 8개 모델(자식)을 순서대로 넣기

    private const string PROP_CHAR = "CharIndex";

    private void Awake()
    {
        ApplyFromOwner(); // 생성 직후 일단 적용
    }

    private void Start()
    {
        ApplyFromOwner(); // 혹시 타이밍 늦어도 한번 더
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // 내 오브젝트의 주인(Owner) 프로퍼티가 바뀌면 다시 적용
        if (photonView == null || photonView.Owner == null) return;
        if (targetPlayer != photonView.Owner) return;
        if (!changedProps.ContainsKey(PROP_CHAR)) return;

        ApplyFromOwner();
    }

    private void ApplyFromOwner()
    {
        if (photonView == null || photonView.Owner == null) return;
        if (models == null || models.Length == 0) return;

        int idx = 0;
        var props = photonView.Owner.CustomProperties;
        if (props != null && props.ContainsKey(PROP_CHAR))
            idx = (int)props[PROP_CHAR];

        // 범위 보호
        if (idx < 0) idx = 0;
        if (idx >= models.Length) idx = models.Length - 1;

        // 모델 하나만 켜기
        for (int i = 0; i < models.Length; i++)
            if (models[i] != null) models[i].SetActive(i == idx);
    }
}
