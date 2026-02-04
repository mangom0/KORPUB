using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class ReadyUI : MonoBehaviourPunCallbacks
{
    [Header("UI - Ready")]
    public Button readyButton;
    public TextMeshProUGUI readyButtonText;
    public TextMeshProUGUI readyCountText;

    [Header("UI - Timer (Top Left)")]
    public TextMeshProUGUI timerText;

    [Header("UI - Lives (Top Right, Local Only)")]
    public TextMeshProUGUI livesTopRightText;

    [Header("UI - Result Panels")]
    public GameObject victoryPanel;
    public GameObject defeatPanel;

    private const string PROP_READY = "Ready";

    private void Start()
    {
        if (readyButton != null)
            readyButton.onClick.AddListener(ToggleReady);

        HideResult();
        RefreshReadyUI();
    }

    private void Update()
    {
        UpdateTimerUI();
    }

    private void ToggleReady()
    {
        if (!PhotonNetwork.InRoom) return;

        bool current = false;
        if (PhotonNetwork.LocalPlayer.CustomProperties != null &&
            PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(PROP_READY))
            current = (bool)PhotonNetwork.LocalPlayer.CustomProperties[PROP_READY];

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PROP_READY, !current } });
    }

    public void SetLocalLives(int lives)
    {
        if (livesTopRightText != null)
            livesTopRightText.text = $" {lives}";
    }

    private void RefreshReadyUI()
    {
        if (!PhotonNetwork.InRoom)
        {
            if (readyCountText != null) readyCountText.text = "";
            return;
        }

        int ready = 0;
        foreach (var p in PhotonNetwork.PlayerList)
        {
            bool r = p.CustomProperties != null && p.CustomProperties.ContainsKey(PROP_READY) && (bool)p.CustomProperties[PROP_READY];
            if (r) ready++;
        }

        if (readyCountText != null)
            readyCountText.text = $"{ready}/{PhotonNetwork.PlayerList.Length} READY";

        bool meReady = PhotonNetwork.LocalPlayer.CustomProperties != null &&
                       PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(PROP_READY) &&
                       (bool)PhotonNetwork.LocalPlayer.CustomProperties[PROP_READY];

        if (readyButtonText != null)
            readyButtonText.text = meReady ? "READY ✓" : "READY";

        // 게임 진행 중이면 Ready 버튼 숨겨도 되고, 남겨도 됨
        // 너는 "다시 Ready 상태로 돌아가기"가 중요하니까, 항상 보이게 두는 걸 추천.
        // (게임 중에도 누를 수는 있지만 게임 로직은 gameStarted일 때 자동시작에 안 씀)
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        if (TofuGameManager.I == null || !PhotonNetwork.InRoom)
        {
            timerText.text = "";
            return;
        }

        // 게임 시작 전
        if (!TofuGameManager.I.IsGameStarted)
        {
            timerText.text = "&";
            return;
        }

        double now = PhotonNetwork.Time;

        // PAUSE 중이면 PAUSE 남은 시간 표시
        if (now < TofuGameManager.I.PauseUntil)
        {
            float left = (float)(TofuGameManager.I.PauseUntil - now);
            if (left < 0) left = 0;
            timerText.text = $"Drink {left:0.0}";
            return;
        }

        float remain = (float)(TofuGameManager.I.Deadline - now);
        if (remain < 0) remain = 0;
        timerText.text = remain.ToString("0.0");
    }

    public override void OnJoinedRoom() => RefreshReadyUI();
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer) => RefreshReadyUI();
    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) => RefreshReadyUI();
    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(PROP_READY))
            RefreshReadyUI();
    }

    // ---------- Result Panels ----------
    public void ShowVictory()
    {
        if (victoryPanel != null) victoryPanel.SetActive(true);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    public void ShowDefeat()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(true);
    }

    public void HideResult()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }
}
