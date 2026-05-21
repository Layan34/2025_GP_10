using UnityEngine;

public class Languagebutton : MonoBehaviour
{
    [SerializeField] private GameObject dropdownObject;

    private void Start()
    {
        if (dropdownObject != null)
            dropdownObject.SetActive(false);
    }

    public void ToggleDropdown()
    {
        if (dropdownObject == null) return;

        dropdownObject.SetActive(!dropdownObject.activeSelf);
    }
}