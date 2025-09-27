using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class CutsceneController : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public Button skipButton;
    public AudioSource videoAudioSource;

    void Start()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Play();
            videoPlayer.loopPointReached += OnVideoEnd;
        }

        skipButton.gameObject.SetActive(false);
        skipButton.onClick.AddListener(SkipVideo);
    }

    void Update()
    {
        // يظهر زر Skip بعد 5 ثواني
        if (videoPlayer != null && videoPlayer.time >= 5f && !skipButton.gameObject.activeSelf)
            skipButton.gameObject.SetActive(true);
    }

    public void SkipVideo()
    {
        KillVideoAudio();

        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();

        // انتقل مباشرة لسين التعليمات
        SceneManager.LoadScene("InstructionsScene");
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        KillVideoAudio();
        SceneManager.LoadScene("InstructionsScene");
    }

    private void KillVideoAudio()
    {
        if (videoAudioSource != null)
        {
            videoAudioSource.Stop();
            videoAudioSource.mute = true;
        }
    }
}
