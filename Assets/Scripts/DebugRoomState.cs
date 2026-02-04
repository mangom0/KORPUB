using UnityEngine;
using Photon.Pun;

public class DebugRoomState : MonoBehaviour
{
    void Start()
    {
        Debug.Log($"[MainScene] InRoom={PhotonNetwork.InRoom}, IsConnected={PhotonNetwork.IsConnected}, State={PhotonNetwork.NetworkClientState}");
    }
}
