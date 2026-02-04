using UnityEngine;
using Photon.Pun;

public class CursorToggleByCtrl : MonoBehaviourPun
{
    [Header("Optional")]
    [Tooltip("카메라 회전 담당 스크립트 (있으면 연결)")]
    public MonoBehaviour lookController; // (optional legacy)
    public PlayerController playerController;

    private bool isUIMode = false;

    private void Start()
    {
        if (!photonView.IsMine) return;
        SetUIMode(false);
    }

    private void Update()
    {
        if (!photonView.IsMine) return;

        bool wantUI = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (wantUI != isUIMode)
        {
            SetUIMode(wantUI);
        }
    }

    private void SetUIMode(bool on)
    {
        isUIMode = on;

        if (on)
        {
            // 마우스 보이게 + 잠금 해제
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            // 마우스 숨김 + 화면 중앙 고정
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        // 🔒 시점 고정
        if (playerController != null)
        {
            playerController.SetLookEnabled(!on);
            playerController.LockCursor(!on);
        }
        else if (lookController != null)
        {
            // fallback: disable whole look script
            lookController.enabled = !on;
        }
    }
}
