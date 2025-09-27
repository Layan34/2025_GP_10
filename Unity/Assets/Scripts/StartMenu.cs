using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class StartMenu : MonoBehaviour
{
    public TMP_InputField nameInput;
    public Button startButton;
    public string nextSceneName = "Characters";

    void Start()
    {
        startButton.interactable = false;
        nameInput.onValueChanged.AddListener(delegate { CheckName(); });
        startButton.onClick.AddListener(StartGame);
    }

    void CheckName()
    {
        startButton.interactable = !string.IsNullOrWhiteSpace(nameInput.text);
    }

    void StartGame()
    {
        PlayerPrefs.SetString("PlayerName", nameInput.text);

        // نستخدم LoadSceneMode.Single عشان يحمّل السين كاملة ويمسح اللي قبلها
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }
}
