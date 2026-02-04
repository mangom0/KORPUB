using Photon.Pun;
using TMPro;
using UnityEngine;

public class TofuPlayer : MonoBehaviourPun
{
    [Header("Speech Bubble (World Space)")]
    public GameObject speechRoot;
    public TextMeshProUGUI speechText;

    [Header("Lives UI (optional) - overhead")]
    public TextMeshProUGUI livesText;

    [Header("Ready Indicator (Green Light)")]
    public GameObject readyLight;

    [Header("Animator")]
    public Animator animator;

    [Header("Disable These When Dead (optional)")]
    public MonoBehaviour[] disableOnDead;

    public int ActorNumber => photonView.OwnerActorNr;

    private float hideAt;

    private void Start()
    {
        if (speechRoot != null) speechRoot.SetActive(false);
        if (readyLight != null) readyLight.SetActive(false);
    }

    private void Update()
    {
        // 입력(1~5)은 로컬 + 게임매니저가 허용할 때만
        if (photonView.IsMine && TofuGameManager.I != null && TofuGameManager.I.CanLocalInput())
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) TofuGameManager.I.RequestAttack(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) TofuGameManager.I.RequestAttack(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) TofuGameManager.I.RequestAttack(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) TofuGameManager.I.RequestAttack(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) TofuGameManager.I.RequestAttack(5);
        }

        // 말풍선 자동 숨김은 모두에게 적용
        if (speechRoot != null && speechRoot.activeSelf && Time.time >= hideAt)
            speechRoot.SetActive(false);
    }

    public void ShowSpeech(string msg)
    {
        if (speechText != null) speechText.text = msg;
        if (speechRoot != null) speechRoot.SetActive(true);
        hideAt = Time.time + 1.2f;
    }

    public void SetLives(int lives)
    {
        if (livesText != null) livesText.text = $"{lives}";
    }

    public void SetReadyVisual(bool ready)
    {
        if (readyLight != null) readyLight.SetActive(ready);
    }

    public void PlayDrink()
    {
        if (animator != null) animator.SetTrigger("Drink");
    }

    public void PlayDie()
    {
        if (animator == null) return;
        animator.SetBool("Die", true);

        // 죽으면 조작/행동 불가
        if (disableOnDead != null)
        {
            foreach (var c in disableOnDead)
            {
                if (c != null) c.enabled = false;
            }
        }
    }
    public void ResetAfterRound()
    {
        if (disableOnDead != null)
        {
            foreach (var c in disableOnDead)
                if (c != null) c.enabled = true;
        }

        if (animator != null)
        {
            // Die(bool) 원복
            animator.SetBool("Die", false);
        }

        if (speechRoot != null) speechRoot.SetActive(false);
    }
}
