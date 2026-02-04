using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviourPun
{
    [Header("Model Root")]
    [SerializeField] private Transform root;              // Player/Root
    [SerializeField] private Transform cameraPivot;       // Player/CameraPivot

    [Header("Look")]
    [SerializeField] private float sensX = 2.0f;
    [SerializeField] private float sensY = 2.0f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 60f;

    private float yaw;
    private float pitch;

    [HideInInspector] public bool canLook = true;

    private void Start()
    {
        ApplySelectedModel();

        if (!photonView.IsMine)
            return;

        SetupFirstPersonCamera();
        LockCursor(true);
    }

    private void Update()
    {
        if (!photonView.IsMine) return;
        if (!canLook) return;
        DoMouseLook();
    }

    private void ApplySelectedModel()
    {
        // 내 로컬의 선택 캐릭터 값은 CustomProperties(CharIndex)에 있음
        int idx = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties != null &&
            PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("CharIndex"))
        {
            idx = (int)PhotonNetwork.LocalPlayer.CustomProperties["CharIndex"];
        }

        if (root == null) return;

        // Root 아래 자식들을 “캐릭터 모델 8개”라고 가정하고 idx만 켬
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i).gameObject;
            child.SetActive(i == idx);
        }
    }

    private void SetupFirstPersonCamera()
    {
        if (cameraPivot == null)
        {
            Debug.LogWarning("[Player] CameraPivot이 비어있음");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[Player] MainCamera 없음");
            return;
        }

        cam.transform.SetParent(cameraPivot);
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;

        yaw = cameraPivot.eulerAngles.y;
        pitch = 0f;
    }

    private void DoMouseLook()
    {
        Vector2 delta = Vector2.zero;
        if (Mouse.current != null)
            delta = Mouse.current.delta.ReadValue();

        yaw += delta.x * sensX * Time.deltaTime;
        pitch -= delta.y * sensY * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 몸통 회전(yaw)
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        // 시선 상하(pitch)
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public void LockCursor(bool v)
    {
        Cursor.lockState = v ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !v;
    }

    public void SetLookEnabled(bool enabled)
    {
        canLook = enabled;
    }
}
