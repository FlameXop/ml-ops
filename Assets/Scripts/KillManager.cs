using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using System.Collections;

public class KillManager : MonoBehaviour
{
    public static KillManager Instance;

    [Header("Audio Settings")]
    public AudioSource killAudioSource;
    public AudioClip[] killSequenceSounds;
    
    [Header("UI Banner Settings")]
    public Animator bannerAnimator; 
    public Image bannerIcon; 
    public Sprite[] tierIcons; 

    private int killStreak = 0;
    private Coroutine bannerRoutine;

    void Awake()
    {
        Instance = this;
    }

    public void RegisterKill()
    {
        killStreak++;

        // --- AUDIO SAFETY CHECK ---
        if (killSequenceSounds != null && killSequenceSounds.Length > 0 && killAudioSource != null)
        {
            int index = Mathf.Min(killStreak - 1, killSequenceSounds.Length - 1);
            killAudioSource.Stop(); 
            killAudioSource.clip = killSequenceSounds[index];
            killAudioSource.Play();
        }

        // --- UI BANNER SAFETY CHECK ---
        if (bannerAnimator != null)
        {
            // Only try to swap icons if you actually assigned them in the Inspector!
            if (bannerIcon != null && tierIcons != null && tierIcons.Length > 0)
            {
                int iconIndex = Mathf.Min(killStreak - 1, tierIcons.Length - 1);
                bannerIcon.sprite = tierIcons[iconIndex];
            }

            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            
            bannerRoutine = StartCoroutine(ShowBannerRoutine());
        }
    }

    public void ResetStreakOnDeath()
    {
        killStreak = 0;
    }

    private IEnumerator ShowBannerRoutine()
    {
        bannerAnimator.Play("Banner_PopIn", 0, 0f);
        yield return new WaitForSeconds(2.5f);
        bannerAnimator.Play("Banner_FadeOut");
    }
}