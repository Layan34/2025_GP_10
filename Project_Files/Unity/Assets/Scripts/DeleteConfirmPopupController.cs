using UnityEngine;

public class DeleteConfirmPopupController : MonoBehaviour
{
    [Header("Popup Root (Panel)")]
    [SerializeField] private GameObject popupRoot; 

    private void Awake()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false); // Hide popup at scene start
    }

    // Displays the confirmation popup
    public void Show()
    {
        if (popupRoot != null)
            popupRoot.SetActive(true);
    }

    // Hides the confirmation popup
    public void Hide()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);
    }
}
