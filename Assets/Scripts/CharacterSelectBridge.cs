using UnityEngine;

public class CharacterSelectBridge : MonoBehaviour
{
    [SerializeField] private CharacterPreviewInScene preview;
    [SerializeField] private LobbyUI lobby;

    public void OnClickBefore()
    {
        if (preview != null) preview.Prev();
    }

    public void OnClickNext()
    {
        if (preview != null) preview.Next();
    }

    public void OnClickSelect()
    {
        if (preview == null || lobby == null) return;
        lobby.ConfirmCharacterAndEnterRoom(preview.GetIndex());
    }

    public void OnClickBack()
    {
        if (lobby != null) lobby.CloseCharacterPanelBackToLobby();
    }
}
