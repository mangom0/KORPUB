using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class TofuGameManager : MonoBehaviourPunCallbacks
{
    public static TofuGameManager I;

    [Header("Game Start")]
    public int requiredPlayers = 5;

    [Header("Timer")]
    public float turnSeconds = 3f;          // 공격 제한 시간
    public float drinkPauseSeconds = 3f;    // 실수 시 멈추는 시간(=drink 연출 시간)

    [Header("Lives")]
    public int maxLives = 3;

    [Header("Round End")]
    public float resultShowSeconds = 5.0f;  // Victory/Defeat 보여줄 시간

    [Header("Seats (seat_0 ~ seat_4)")]
    public Transform[] seatRoots = new Transform[5];

    private const string PROP_SEAT = "SeatIndex";
    private const string PROP_READY = "Ready";

    private bool gameStarted = false;
    private bool roundEnded = false;

    private int currentAttackerActor = -1;

    // 타이머 상태(전원 동기화)
    private double pauseUntilNetworkTime = 0; // now < pauseUntil 이면 타이머/입력 정지
    private double deadlineNetworkTime = 0;   // now >= pauseUntil 일 때 남은 시간 = deadline - now

    private readonly Dictionary<int, int> livesByActor = new Dictionary<int, int>();

    // ---------- Unity ----------
    private void Awake()
    {
        I = this;
    }

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            InitLivesIfNeeded();
            SyncAllReadyVisualsToEveryone();
            TryAutoStartIfReady();
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom) return;

        // 게임 시작 전이면(마스터) 자동 시작 조건 계속 체크
        if (PhotonNetwork.IsMasterClient && !gameStarted && !roundEnded)
        {
            TryAutoStartIfReady();
            return;
        }

        if (!gameStarted) return;

        // 정지 시간에는 타임아웃 판정도 하지 않음
        if (IsPausedNow()) return;

        // (마스터) 타임아웃 = 현재 공격자 목숨 -1 + drink 정지 + 같은 사람부터 다시
        if (PhotonNetwork.IsMasterClient && currentAttackerActor != -1)
        {
            if (PhotonNetwork.Time > deadlineNetworkTime)
            {
                ApplyLifeDelta(currentAttackerActor, -1, "timeout");
                CheckRoundEndIfNeeded();
                if (!gameStarted) return;

                if (IsAlive(currentAttackerActor))
                    Master_StartPausedTurn(currentAttackerActor);
                else
                    Master_StartRunningTurn(GetFirstAliveActor());
            }
        }
    }

    // ---------- Public API (called by local player) ----------
    public void RequestAttack(int keyIndex)
    {
        if (!PhotonNetwork.InRoom) return;
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        photonView.RPC(nameof(RPC_RequestAttack), RpcTarget.MasterClient, me, keyIndex);
    }

    // 로컬 입력(1~5)을 지금 허용할지(게임시작 전 / 정지 / 죽음은 금지)
    public bool CanLocalInput()
    {
        if (!PhotonNetwork.InRoom) return false;
        if (!gameStarted) return false;
        if (roundEnded) return false;
        if (IsPausedNow()) return false;

        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        return IsAlive(me);
    }

    public bool IsGameStarted => gameStarted;
    public bool IsRoundEnded => roundEnded;
    public double PauseUntil => pauseUntilNetworkTime;
    public double Deadline => deadlineNetworkTime;
    public int CurrentAttacker => currentAttackerActor;

    // ---------- Master authoritative logic ----------
    [PunRPC]
    private void RPC_RequestAttack(int requesterActor, int keyIndex, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!gameStarted) return;
        if (roundEnded) return;

        InitLivesIfNeeded();
        if (!IsAlive(requesterActor)) return;

        // 정지 중에는 입력 무시
        if (IsPausedNow()) return;

        // 라운드 종료 상태면 더 진행하지 않음
        CheckRoundEndIfNeeded();
        if (!gameStarted) return;

        // 말풍선은 모든 입력에서 띄움
        photonView.RPC(nameof(RPC_ShowSpeech), RpcTarget.All, requesterActor, KeyToPhrase(keyIndex));

        int aliveCount = GetAliveActorsOrderedBySeat().Count;

        // ✅ 3명 남았을 때: 한모(1) / 다섯모(5) = 술(패널티)
        if (aliveCount == 3 && (keyIndex == 1 || keyIndex == 5))
        {
            ApplyLifeDelta(requesterActor, -1, "3p_wrong_15");
            CheckRoundEndIfNeeded();
            if (!gameStarted) return;

            if (IsAlive(requesterActor))
                Master_StartPausedTurn(requesterActor);
            else
                Master_StartRunningTurn(GetFirstAliveActor());
            return;
        }

        // ✅ 자기 지칭(3) = 술(패널티)
        if (keyIndex == 3)
        {
            ApplyLifeDelta(requesterActor, -1, "self");
            CheckRoundEndIfNeeded();
            if (!gameStarted) return;

            if (IsAlive(requesterActor))
                Master_StartPausedTurn(requesterActor);
            else
                Master_StartRunningTurn(GetFirstAliveActor());
            return;
        }

        // ✅ 자기 차례 아닌데 공격 시도 = 술(패널티) + 턴 강탈(본인부터)
        if (requesterActor != currentAttackerActor)
        {
            ApplyLifeDelta(requesterActor, -1, "out_of_turn");
            CheckRoundEndIfNeeded();
            if (!gameStarted) return;

            if (IsAlive(requesterActor))
                Master_StartPausedTurn(requesterActor);
            else
                Master_StartRunningTurn(GetFirstAliveActor());
            return;
        }

        // 타겟 계산 (죽은 사람 자동 제외)
        if (!TryResolveTarget(requesterActor, keyIndex, out int targetActor))
        {
            // 타겟이 성립 안 하면 -> 현재 공격자 그대로 재시작
            Master_StartRunningTurn(requesterActor);
            return;
        }

        // 정상 공격 성공 -> targetActor가 다음 턴 + 타이머 3초 리셋
        Master_StartRunningTurn(targetActor);
    }

    // ---------- Turn + Timer ----------
    private bool IsPausedNow()
    {
        return PhotonNetwork.Time < pauseUntilNetworkTime;
    }

    private void Master_StartRunningTurn(int attackerActor)
    {
        attackerActor = GetAliveFallback(attackerActor);
        currentAttackerActor = attackerActor;

        double now = PhotonNetwork.Time;
        pauseUntilNetworkTime = now;                  // 정지 없음
        deadlineNetworkTime = now + turnSeconds;      // 3초 카운트다운

        photonView.RPC(nameof(RPC_SyncState), RpcTarget.All,
            currentAttackerActor, pauseUntilNetworkTime, deadlineNetworkTime);

        CheckRoundEndIfNeeded();
    }

    private void Master_StartPausedTurn(int attackerActor)
    {
        attackerActor = GetAliveFallback(attackerActor);
        currentAttackerActor = attackerActor;

        double now = PhotonNetwork.Time;
        pauseUntilNetworkTime = now + drinkPauseSeconds;                 // 3초 멈춤
        deadlineNetworkTime = pauseUntilNetworkTime + turnSeconds;       // 멈춘 뒤 3초

        photonView.RPC(nameof(RPC_SyncState), RpcTarget.All,
            currentAttackerActor, pauseUntilNetworkTime, deadlineNetworkTime);

        CheckRoundEndIfNeeded();
    }

    [PunRPC]
    private void RPC_SyncState(int attackerActor, double pauseUntil, double deadline)
    {
        currentAttackerActor = attackerActor;
        pauseUntilNetworkTime = pauseUntil;
        deadlineNetworkTime = deadline;
    }

    [PunRPC]
    private void RPC_SetGameStarted(bool started, int firstAttackerActor, double pauseUntil, double deadline)
    {
        gameStarted = started;
        roundEnded = false;

        currentAttackerActor = firstAttackerActor;
        pauseUntilNetworkTime = pauseUntil;
        deadlineNetworkTime = deadline;
    }

    // ---------- Target resolve ----------
    // key mapping:
    // 1: left2  ( +2 )
    // 2: left1  ( +1 )
    // 4: right1 ( -1 )
    // 5: right2 ( -2 )
    private bool TryResolveTarget(int attackerActor, int keyIndex, out int targetActor)
    {
        targetActor = -1;

        int offset = 0;
        switch (keyIndex)
        {
            case 1: offset = +2; break; // 두부한모 = 왼쪽 2칸
            case 2: offset = +1; break; // 두부두모 = 왼쪽 1칸
            case 4: offset = -1; break; // 두부네모 = 오른쪽 1칸
            case 5: offset = -2; break; // 두부다섯모 = 오른쪽 2칸
            default: return false;
        }

        List<int> aliveActors = GetAliveActorsOrderedBySeat();
        if (aliveActors.Count < 2) return false;

        int idx = aliveActors.IndexOf(attackerActor);
        if (idx < 0) return false;

        int n = aliveActors.Count;
        int t = (idx + offset) % n;
        if (t < 0) t += n;

        targetActor = aliveActors[t];
        if (targetActor == attackerActor) return false;

        return true;
    }

    private List<int> GetAliveActorsOrderedBySeat()
    {
        var list = new List<(int actor, int seat)>();

        foreach (var p in PhotonNetwork.PlayerList)
        {
            int actor = p.ActorNumber;
            if (!IsAlive(actor)) continue;

            if (p.CustomProperties != null && p.CustomProperties.ContainsKey(PROP_SEAT))
            {
                int seat = (int)p.CustomProperties[PROP_SEAT];
                list.Add((actor, seat));
            }
        }

        list.Sort((a, b) => a.seat.CompareTo(b.seat));

        var actors = new List<int>();
        foreach (var x in list) actors.Add(x.actor);
        return actors;
    }

    private string KeyToPhrase(int keyIndex)
    {
        switch (keyIndex)
        {
            case 1: return "두부한모";
            case 2: return "두부두모";
            case 3: return "두부세모";
            case 4: return "두부네모";
            case 5: return "두부다섯모";
            default: return "두부?";
        }
    }

    // ---------- Lives ----------
    private void InitLivesIfNeeded()
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (!livesByActor.ContainsKey(p.ActorNumber))
                livesByActor[p.ActorNumber] = maxLives;
        }
    }

    private bool IsAlive(int actor)
    {
        if (!livesByActor.ContainsKey(actor)) livesByActor[actor] = maxLives;
        return livesByActor[actor] > 0;
    }

    private int GetFirstAliveActor()
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            int a = p.ActorNumber;
            if (!livesByActor.ContainsKey(a)) livesByActor[a] = maxLives;
            if (livesByActor[a] > 0) return a;
        }
        return -1;
    }

    private int GetAliveFallback(int preferred)
    {
        if (preferred != -1 && IsAlive(preferred)) return preferred;
        return GetFirstAliveActor();
    }

    private void ApplyLifeDelta(int actor, int delta, string reason)
    {
        if (!livesByActor.ContainsKey(actor)) livesByActor[actor] = maxLives;

        int before = livesByActor[actor];
        int after = Mathf.Max(0, before + delta);
        livesByActor[actor] = after;

        photonView.RPC(nameof(RPC_SyncLives), RpcTarget.All, actor, after, reason);

        if (delta < 0)
        {
            if (after > 0)
            {
                photonView.RPC(nameof(RPC_PlayDrink), RpcTarget.All, actor);
            }
            else if (after == 0 && before > 0)
            {
                photonView.RPC(nameof(RPC_PlayDie), RpcTarget.All, actor);
            }
        }

        if (PhotonNetwork.IsMasterClient && after == 0 && before > 0)
            CheckRoundEndIfNeeded();
    }

    [PunRPC]
    private void RPC_SyncLives(int actor, int lives, string reason)
    {
        foreach (var tp in FindObjectsOfType<TofuPlayer>())
        {
            if (tp.ActorNumber == actor)
            {
                tp.SetLives(lives);
                break;
            }
        }

        // 로컬 HUD(우상단 목숨) 갱신
        if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.ActorNumber == actor)
        {
            var ui = FindObjectOfType<ReadyUI>();
            if (ui != null) ui.SetLocalLives(lives);
        }
    }

    [PunRPC]
    private void RPC_PlayDrink(int actor)
    {
        foreach (var tp in FindObjectsOfType<TofuPlayer>())
            if (tp.ActorNumber == actor) { tp.PlayDrink(); break; }
    }

    [PunRPC]
    private void RPC_PlayDie(int actor)
    {
        foreach (var tp in FindObjectsOfType<TofuPlayer>())
            if (tp.ActorNumber == actor) { tp.PlayDie(); break; }
    }

    // ---------- Visual / message ----------
    [PunRPC]
    private void RPC_ShowSpeech(int actorNumber, string phrase)
    {
        foreach (var tp in FindObjectsOfType<TofuPlayer>())
        {
            if (tp.ActorNumber == actorNumber)
            {
                tp.ShowSpeech(phrase);
                break;
            }
        }
    }

    // ---------- Ready -> auto start + head light ----------
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(PROP_READY))
        {
            bool ready = targetPlayer.CustomProperties != null &&
                         targetPlayer.CustomProperties.ContainsKey(PROP_READY) &&
                         (bool)targetPlayer.CustomProperties[PROP_READY];

            foreach (var tp in FindObjectsOfType<TofuPlayer>())
                if (tp.ActorNumber == targetPlayer.ActorNumber) { tp.SetReadyVisual(ready); break; }

            if (PhotonNetwork.IsMasterClient && !gameStarted && !roundEnded)
                TryAutoStartIfReady();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient && !gameStarted && !roundEnded)
            TryAutoStartIfReady();

        SyncAllReadyVisualsToEveryone();
    }

    public override void OnJoinedRoom()
    {
        SyncAllReadyVisualsToEveryone();
    }

    private void SyncAllReadyVisualsToEveryone()
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            bool ready = p.CustomProperties != null &&
                         p.CustomProperties.ContainsKey(PROP_READY) &&
                         (bool)p.CustomProperties[PROP_READY];

            foreach (var tp in FindObjectsOfType<TofuPlayer>())
                if (tp.ActorNumber == p.ActorNumber) { tp.SetReadyVisual(ready); break; }
        }
    }

    private void TryAutoStartIfReady()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (gameStarted) return;
        if (roundEnded) return;

        if (PhotonNetwork.PlayerList.Length != requiredPlayers) return;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            bool ready = p.CustomProperties != null &&
                         p.CustomProperties.ContainsKey(PROP_READY) &&
                         (bool)p.CustomProperties[PROP_READY];
            if (!ready) return;
        }

        // ✅ 게임 시작: 마스터부터 시작
        gameStarted = true;
        roundEnded = false;

        int firstAttacker = PhotonNetwork.LocalPlayer.ActorNumber;
        double now = PhotonNetwork.Time;

        currentAttackerActor = firstAttacker;
        pauseUntilNetworkTime = now;
        deadlineNetworkTime = now + turnSeconds;

        photonView.RPC(nameof(RPC_SetGameStarted), RpcTarget.All, true, firstAttacker, pauseUntilNetworkTime, deadlineNetworkTime);
    }

    // ---------- Round End / Reset ----------
    private void CheckRoundEndIfNeeded()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!gameStarted) return;
        if (roundEnded) return;

        int aliveCount = 0;
        foreach (var p in PhotonNetwork.PlayerList)
            if (IsAlive(p.ActorNumber)) aliveCount++;

        if (aliveCount <= 2)
        {
            roundEnded = true;
            gameStarted = false; // 결과 보여주는 동안 입력 막기

            photonView.RPC(nameof(RPC_ShowResultPanels), RpcTarget.All);

            Invoke(nameof(Master_QuitGame), resultShowSeconds);
        }
    }
    private void Master_QuitGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC(nameof(RPC_QuitGame), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

    [PunRPC]
    private void RPC_ShowResultPanels()
    {
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        bool win = IsAlive(me);

        var ui = FindObjectOfType<ReadyUI>();
        if (ui != null)
        {
            if (win) ui.ShowVictory();
            else ui.ShowDefeat();
        }
    }

    private void Master_ResetToLobby()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 마스터 내부 상태 리셋
        roundEnded = false;
        currentAttackerActor = -1;
        pauseUntilNetworkTime = 0;
        deadlineNetworkTime = 0;

        // 목숨 초기화
        livesByActor.Clear();
        InitLivesIfNeeded();

        // 모든 클라에게: UI/Ready/플레이어 상태 리셋
        photonView.RPC(nameof(RPC_ResetRoundAllClients), RpcTarget.All, maxLives);

        // 이제 다시 Ready 모이면 자동 시작됨
    }

    [PunRPC]
    private void RPC_ResetRoundAllClients(int livesReset)
    {
        // 로컬 결과 UI 숨기기 + 로컬 Ready false로
        var ui = FindObjectOfType<ReadyUI>();
        if (ui != null) ui.HideResult();

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PROP_READY, false } });

        // 타이머/상태 리셋(각 클라)
        gameStarted = false;
        roundEnded = false;
        currentAttackerActor = -1;
        pauseUntilNetworkTime = 0;
        deadlineNetworkTime = 0;

        // 로컬 딕셔너리도 리셋
        livesByActor.Clear();
        foreach (var p in PhotonNetwork.PlayerList)
            livesByActor[p.ActorNumber] = livesReset;

        // 플레이어 프리팹 상태/컨트롤 복구, 목숨 UI 갱신
        foreach (var tp in FindObjectsOfType<TofuPlayer>())
        {
            tp.SetLives(livesReset);
            tp.SetReadyVisual(false);
            tp.ResetAfterRound();
        }

        // 내 우상단 목숨 갱신
        if (ui != null) ui.SetLocalLives(livesReset);
    }
}
