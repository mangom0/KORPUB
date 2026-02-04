using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Collections.Generic;

public class GameManager : MonoBehaviourPunCallbacks
{
    [Header("Prefabs")]
    [SerializeField] private string playerPrefabName = "Player";

    [Header("Seat Positions")]
    [SerializeField] private Transform[] seatPositions = new Transform[5];

    private const string PROP_SEAT = "SeatIndex";
    private bool spawnedLocal = false;
    private bool requestedSeat = false;

    private void Awake()
    {
        Debug.Log("[GM] Awake");
    }

    private void Start()
    {
        // ⭐ 중요: 메인씬에 들어왔을 때 이미 방에 들어가 있는 상태일 수 있음
        TryRequestSeatIfInRoom();
    }

    private void TryRequestSeatIfInRoom()
    {
        if (requestedSeat) return;
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom) return;

        if (photonView == null)
        {
            Debug.LogError("[GM] PhotonView가 없습니다. GameManager에 PhotonView 추가하세요.");
            return;
        }

        requestedSeat = true;
        Debug.Log("[GM] InRoom already -> RequestSeat");

        photonView.RPC(nameof(RPC_RequestSeat), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    public override void OnJoinedRoom()
    {
        // 혹시 GameManager가 DontDestroy로 유지되는 구조도 대비
        TryRequestSeatIfInRoom();
    }

    [PunRPC]
    private void RPC_RequestSeat(int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Player p = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (p == null) return;

        AssignSeatIfNeeded(p);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        AssignSeatIfNeeded(newPlayer);
    }

    private void AssignSeatIfNeeded(Player p)
    {
        if (p.CustomProperties != null && p.CustomProperties.ContainsKey(PROP_SEAT))
            return;

        int seat = FindFirstFreeSeat();
        if (seat < 0)
        {
            Debug.LogError("[GM] 빈 좌석이 없습니다.");
            return;
        }

        var props = new Hashtable { { PROP_SEAT, seat } };
        p.SetCustomProperties(props);
    }

    private int FindFirstFreeSeat()
    {
        HashSet<int> used = new HashSet<int>();

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.CustomProperties != null && p.CustomProperties.ContainsKey(PROP_SEAT))
                used.Add((int)p.CustomProperties[PROP_SEAT]);
        }

        for (int i = 0; i < seatPositions.Length; i++)
        {
            if (seatPositions[i] != null && !used.Contains(i))
                return i;
        }

        return -1;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != PhotonNetwork.LocalPlayer) return;
        if (!changedProps.ContainsKey(PROP_SEAT)) return;
        if (spawnedLocal) return;

        int seatIndex = (int)changedProps[PROP_SEAT];
        Debug.Log($"[GM] SeatIndex received: {seatIndex}");

        SpawnLocalPlayerAtSeat(seatIndex);
    }

    private void SpawnLocalPlayerAtSeat(int seatIndex)
    {
        if (seatPositions == null || seatPositions.Length == 0)
        {
            Debug.LogError("[GM] seatPositions 비어있음");
            return;
        }

        if (seatIndex < 0 || seatIndex >= seatPositions.Length || seatPositions[seatIndex] == null)
        {
            Debug.LogError($"[GM] seatPositions[{seatIndex}] 비어있음");
            return;
        }

        Transform sp = seatPositions[seatIndex];

        Debug.Log($"[GM] SpawnLocalPlayerAtSeat({seatIndex}) -> {playerPrefabName}");

        GameObject obj = PhotonNetwork.Instantiate(playerPrefabName, sp.position, sp.rotation);
        obj.transform.SetPositionAndRotation(sp.position, sp.rotation);

        spawnedLocal = true;
    }
}
