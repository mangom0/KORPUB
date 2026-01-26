using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    public InputField nicknameInput;
    public InputField roomNameInput;
    public Text statusText;
    public Button createButton;
    public Button joinButton;
    public Button quickStartButton;

    [Header("Photon Settings")]
    [SerializeField] private string gameVersion = "1";
    [SerializeField] private string quickStartRoomName = "TofuQuickRoom";
    [SerializeField] private string gameSceneName = "MainScene"; // 실제 게임 씬 이름으로 바꿔줘

    private bool isConnectedToMaster = false;
    private bool isInLobby = false;

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;

        SetButtonsInteractable(false);

        if (statusText != null)
            statusText.text = "Connecting to Photon...";

        PhotonNetwork.ConnectUsingSettings();
    }

    // ----------------- Photon 콜백 -----------------

    public override void OnConnectedToMaster()
    {
        isConnectedToMaster = true;

        if (statusText != null)
            statusText.text = "Connected. Joining lobby...";

        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        isInLobby = true;

        if (statusText != null)
            statusText.text = "Lobby joined! You can create or join a room.";

        SetButtonsInteractable(true);
    }

    public override void OnJoinedRoom()
    {
        if (statusText != null)
        {
            statusText.text = $"Joined room: {PhotonNetwork.CurrentRoom.Name} " +
                              $"({PhotonNetwork.CurrentRoom.PlayerCount} players)";
        }

        // 로비 씬에서 게임 씬으로 이동
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (statusText != null)
            statusText.text = $"Create room failed: {message}";

        SetButtonsInteractable(true);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (statusText != null)
            statusText.text = $"Join room failed: {message}";

        SetButtonsInteractable(true);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        isConnectedToMaster = false;
        isInLobby = false;

        SetButtonsInteractable(false);

        if (statusText != null)
            statusText.text = $"Disconnected: {cause}. Reconnecting...";

        PhotonNetwork.ConnectUsingSettings();
    }

    // ----------------- 버튼에서 호출하는 함수들 -----------------

    public void OnClick_CreateRoom()
    {
        if (!CanUseRoomButtons()) return;

        ApplyNickname();

        string roomName = (roomNameInput != null && !string.IsNullOrEmpty(roomNameInput.text))
            ? roomNameInput.text
            : "TofuRoom";

        RoomOptions options = new RoomOptions { MaxPlayers = 8 };

        if (statusText != null)
            statusText.text = $"Creating room: {roomName}";

        SetButtonsInteractable(false);
        PhotonNetwork.CreateRoom(roomName, options);
    }

    public void OnClick_JoinRoom()
    {
        if (!CanUseRoomButtons()) return;

        ApplyNickname();

        if (roomNameInput == null || string.IsNullOrEmpty(roomNameInput.text))
        {
            if (statusText != null)
                statusText.text = "방 이름을 입력하세요.";
            return;
        }

        string roomName = roomNameInput.text;

        if (statusText != null)
            statusText.text = $"Joining room: {roomName}";

        SetButtonsInteractable(false);
        PhotonNetwork.JoinRoom(roomName);
    }

    public void OnClick_QuickStart()
    {
        if (!CanUseRoomButtons()) return;

        ApplyNickname();

        RoomOptions options = new RoomOptions { MaxPlayers = 8 };

        if (statusText != null)
            statusText.text = "Quick start...";

        SetButtonsInteractable(false);
        PhotonNetwork.JoinOrCreateRoom(quickStartRoomName, options, TypedLobby.Default);
    }

    // ----------------- 내부 유틸 함수 -----------------

    void ApplyNickname()
    {
        if (nicknameInput != null && !string.IsNullOrEmpty(nicknameInput.text))
        {
            PhotonNetwork.NickName = nicknameInput.text;
        }
        else
        {
            PhotonNetwork.NickName = "Player_" + Random.Range(0, 9999);
        }
    }

    bool CanUseRoomButtons()
    {
        if (!isConnectedToMaster)
        {
            if (statusText != null)
                statusText.text = "서버에 연결 중입니다. 잠시만 기다려 주세요.";
            return false;
        }

        if (!isInLobby)
        {
            if (statusText != null)
                statusText.text = "로비에 진입 중입니다. 잠시만 기다려 주세요.";
            return false;
        }

        return true;
    }

    void SetButtonsInteractable(bool value)
    {
        if (createButton != null) createButton.interactable = value;
        if (joinButton != null) joinButton.interactable = value;
        if (quickStartButton != null) quickStartButton.interactable = value;
    }
}
