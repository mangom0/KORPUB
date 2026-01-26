using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using PhotonPlayer = Photon.Realtime.Player;

public class PlayerSeatController : MonoBehaviourPunCallbacks
{
    public Animator animator;
    public string sitBoolName = "isSitting";

    void Start()
    {
        TrySeatPlayer();
    }

    void TrySeatPlayer()
    {
        PhotonPlayer owner = photonView.Owner;

        if (owner.CustomProperties.TryGetValue("seatIndex", out object seatObj))
        {
            int seatIndex = (int)seatObj;
            Debug.Log($"[SeatMove] {owner.NickName} seatIndex={seatIndex}");

            Transform seat = GameManager.Instance.seatPoints[seatIndex];

            if (seat == null)
            {
                Debug.LogError($"[SeatMove] seatPoints[{seatIndex}] is null!");
                return;
            }

            transform.position = seat.position;
            transform.rotation = seat.rotation;

            if (animator != null)
                animator.SetBool(sitBoolName, true);
        }
        else
        {
            // 아직 seatIndex 안 들어왔으면 잠시 후 재시도
            Invoke(nameof(TrySeatPlayer), 0.1f);
        }
    }
}
