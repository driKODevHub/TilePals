using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LevelSelectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform buttonsContainer;
    [SerializeField] private GameObject locationButtonPrefab;
    [SerializeField] private Button backButton;

    private List<GameObject> _spawnedButtons = new List<GameObject>();
    private bool _isInitialized = false;

    private void Awake()
    {
        if (backButton)
        {
            backButton.onClick.AddListener(() =>
            {
                UIManager.Instance.OnLevelSelectionBack();
            });
        }
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);

        if (isActive && !_isInitialized)
        {
            GenerateLocationButtons();
            _isInitialized = true;
        }
    }

    private void GenerateLocationButtons()
    {
        foreach (var btn in _spawnedButtons) Destroy(btn);
        _spawnedButtons.Clear();

        var locations = GameManager.Instance.GetAvailableLocations();
        if (locations == null) return;

        for (int i = 0; i < locations.Count; i++)
        {
            int index = i;
            LevelCollectionSO location = locations[i];
            if (location == null) continue;

            GameObject btnObj = Instantiate(locationButtonPrefab, buttonsContainer);
            _spawnedButtons.Add(btnObj);

            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = location.locationName;
            }

            Button btnComp = btnObj.GetComponent<Button>();
            btnComp.onClick.AddListener(() =>
            {
                OnLocationButtonClicked(index);
            });
        }
    }

    private void OnLocationButtonClicked(int locationIndex)
    {
        GameManager.Instance.SelectLocation(locationIndex);
    }
}
