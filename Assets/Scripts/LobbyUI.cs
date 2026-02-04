using Photon.Pun;
using ExitGames.Client.Photon;
using TMPro;
using UnityEngine;

using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField nickNameInput;
    [SerializeField] private TMP_InputField roomNameInput;

    [Header("Panels")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject characterPanel;

    [Header("Refs")]
    [SerializeField] private Launcher launcher;
    [SerializeField] private CharacterPreviewInScene preview;

    private enum Pending { None, Create, Join, Quick }
    private Pending pending = Pending.None;
    private string pendingRoomName = "";

    private const string PROP_CHAR = "CharIndex";

    private void Start()
    {
        characterPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    // Create 버튼 OnClick
    public void OnClickCreate()
    {
        ApplyNickname();

        pending = Pending.Create;
        pendingRoomName = roomNameInput != null ? roomNameInput.text : "";
        OpenCharacterPanel();
    }

    // Join 버튼 OnClick
    public void OnClickJoin()
    {
        ApplyNickname();

        pending = Pending.Join;
        pendingRoomName = roomNameInput != null ? roomNameInput.text : "";
        OpenCharacterPanel();
    }

    // Quick 버튼 OnClick
    public void OnClickQuick()
    {
        ApplyNickname();

        pending = Pending.Quick;
        pendingRoomName = "";
        OpenCharacterPanel();
    }

    private void ApplyNickname()
    {
        if (nickNameInput == null) return;
        if (!string.IsNullOrWhiteSpace(nickNameInput.text))
            PhotonNetwork.NickName = nickNameInput.text;
    }

    private void OpenCharacterPanel()
    {
        lobbyPanel.SetActive(false);
        characterPanel.SetActive(true);
        if (preview != null) preview.SetVisible(true);
    }

    public void CloseCharacterPanelBackToLobby()
    {
        if (preview != null) preview.SetVisible(false);
        characterPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    // Select 버튼에서 호출 (CharacterSelectBridge가 호출해도 되고, 버튼이 직접 호출해도 됨)
    public void ConfirmCharacterAndEnterRoom(int charIndex)
    {
        // 1) 캐릭터 선택 값을 내 CustomProperties로 저장
        var props = new Hashtable { { PROP_CHAR, charIndex } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // 2) pending action -> Launcher에 전달 후 실행
        switch (pending)
        {
            case Pending.Create:
                launcher.SetPendingCreate(pendingRoomName);
                break;
            case Pending.Join:
                launcher.SetPendingJoin(pendingRoomName);
                break;
            case Pending.Quick:
                launcher.SetPendingQuick();
                break;
        }

        launcher.ExecutePending();
    }
}
