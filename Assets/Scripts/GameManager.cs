using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

using Hashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;

    [Header("Booth Seat Points")]
    public Transform[] seatPoints;   // Seat_0 ~ Seat_n

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[GameManager] Not in a room!");
            return;
        }

        Debug.Log($"[GameManager] Start. Room={PhotonNetwork.CurrentRoom.Name}, " +
                  $"Nick={PhotonNetwork.NickName}, IsMaster={PhotonNetwork.IsMasterClient}");

        // 마스터가 현재 방 인원 기준으로 좌석 배정
        if (PhotonNetwork.IsMasterClient)
        {
            AssignSeatsToPlayers();
        }

        // **모든 클라이언트가 자기 캐릭터를 하나씩 소환**
        SpawnLocalPlayer();
    }

    public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
    {
        // 누군가 새로 들어오면 마스터가 다시 좌석 재배정
        if (PhotonNetwork.IsMasterClient)
        {
            AssignSeatsToPlayers();
        }
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        // 누군가 나가면 마스터가 남은 사람들 기준으로 다시 좌석 재배정
        if (PhotonNetwork.IsMasterClient)
        {
            AssignSeatsToPlayers();
        }
    }

    void AssignSeatsToPlayers()
    {
        PhotonPlayer[] players = PhotonNetwork.PlayerList;

        Debug.Log($"[SeatAssign] start. players={players.Length}, seats={seatPoints.Length}");

        for (int i = 0; i < players.Length && i < seatPoints.Length; i++)
        {
            PhotonPlayer p = players[i];

            Hashtable hash = new Hashtable
            {
                ["seatIndex"] = i,
                ["isBot"] = false
            };

            p.SetCustomProperties(hash);

            Debug.Log($"[SeatAssign] {p.NickName} (Actor {p.ActorNumber}) -> seat {i}");
        }
    }

    void SpawnLocalPlayer()
    {
        Debug.Log($"[GameManager] SpawnLocalPlayer for {PhotonNetwork.NickName}");

        // Resources 안의 프리팹 이름과 정확히 맞춰야 함
        PhotonNetwork.Instantiate("SM_Chr_Bartender_Female_01", Vector3.zero, Quaternion.identity);
    }
}
