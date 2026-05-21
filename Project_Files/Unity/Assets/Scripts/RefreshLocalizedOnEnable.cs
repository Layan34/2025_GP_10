using UnityEngine;
using UnityEngine.Localization.Components;

public class RefreshLocalizedOnEnable : MonoBehaviour
{
    void OnEnable()
    {
        var localizers = GetComponentsInChildren<LocalizeStringEvent>(true);
        foreach (var l in localizers)
            l.RefreshString(); 
    }
}
