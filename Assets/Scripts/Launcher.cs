using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class Launcher : MonoBehaviourPunCallbacks
{
    [Header("Photon")]
    [SerializeField] private string gameVersion = "1";

    [Header("Scene")]
    [SerializeField] private string mainSceneName = "MainScene";

    private enum PendingAction { None, Create, Join, Quick }
    private PendingAction pending = PendingAction.None;
    private string pendingRoomName = "";

    private void Start()
    {
        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.AutomaticallySyncScene = true;

        if (!PhotonNetwork.IsConnected)
            PhotonNetwork.ConnectUsingSettings();
    }

    // LobbyUI가 호출
    public void SetPendingCreate(string roomName)
    {
        pending = PendingAction.Create;
        pendingRoomName = roomName;
        EnsureConnected();
    }

    public void SetPendingJoin(string roomName)
    {
        pending = PendingAction.Join;
        pendingRoomName = roomName;
        EnsureConnected();
    }

    public void SetPendingQuick()
    {
        pending = PendingAction.Quick;
        pendingRoomName = "";
        EnsureConnected();
    }

    // 캐릭터 선택 끝나고 LobbyUI가 호출
    public void ExecutePending()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            EnsureConnected();
            return;
        }

        switch (pending)
        {
            case PendingAction.Create:
                if (string.IsNullOrWhiteSpace(pendingRoomName))
                {
                    Debug.LogWarning("RoomName이 비어있어요.");
                    pending = PendingAction.None;
                    return;
                }
                PhotonNetwork.CreateRoom(
                    pendingRoomName,
                    new RoomOptions { MaxPlayers = 5 },
                    TypedLobby.Default
                );
                break;

            case PendingAction.Join:
                if (string.IsNullOrWhiteSpace(pendingRoomName))
                {
                    Debug.LogWarning("RoomName이 비어있어요.");
                    pending = PendingAction.None;
                    return;
                }
                PhotonNetwork.JoinRoom(pendingRoomName);
                break;

            case PendingAction.Quick:
                PhotonNetwork.JoinRandomRoom();
                break;
        }
    }

    private void EnsureConnected()
    {
        if (!PhotonNetwork.IsConnected)
            PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        // 아직 캐릭터 선택 전이면 그냥 대기
        Debug.Log("[Launcher] ConnectedToMaster");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        // 랜덤 실패 시 자동 방 생성
        string autoRoom = "Room_" + Random.Range(1000, 9999);
        PhotonNetwork.CreateRoom(autoRoom, new RoomOptions { MaxPlayers = 5 }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[Launcher] JoinedRoom -> Load MainScene");
        pending = PendingAction.None;
        PhotonNetwork.LoadLevel(mainSceneName);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"CreateRoomFailed: {message}");
        pending = PendingAction.None;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"JoinRoomFailed: {message}");
        pending = PendingAction.None;
    }
}
