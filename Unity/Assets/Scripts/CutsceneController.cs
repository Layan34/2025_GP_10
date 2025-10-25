using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class CutsceneController : MonoBehaviour
{
    public VideoPlayer videoPlayer;   // Reference to the video player component
    public Button skipButton;         // Button that allows skipping the cutscene

    void Start()
    {
        if (videoPlayer != null)
        {
            // Play video audio directly (no separate AudioSource component)
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

            // Mute all extra audio tracks except the main one
            for (ushort i = 1; i < videoPlayer.clip.audioTrackCount; i++)
                videoPlayer.SetDirectAudioMute(i, true);

            // Ensure main track is unmuted and at full volume
            videoPlayer.SetDirectAudioMute(0, false);
            videoPlayer.SetDirectAudioVolume(0, 1f);

            // Begin video playback and detect when it ends
            videoPlayer.Play();
            videoPlayer.loopPointReached += OnVideoEnd;
        }

        // Hide skip button initially and assign its click action
        skipButton.gameObject.SetActive(false);
        skipButton.onClick.AddListener(SkipVideo);
    }

    void Update()
    {
        // Show skip button after 5 seconds of video playback
        if (videoPlayer != null && videoPlayer.time >= 5f && !skipButton.gameObject.activeSelf)
            skipButton.gameObject.SetActive(true);
    }

    // Stops the video manually and moves to the next scene
    public void SkipVideo()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();

        SceneManager.LoadScene("InstructionsScene");
    }

    // Automatically called when video finishes playing
    private void OnVideoEnd(VideoPlayer vp)
    {
        SceneManager.LoadScene("InstructionsScene");
    }
}
