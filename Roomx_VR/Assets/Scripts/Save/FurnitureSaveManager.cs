using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.IO;

public class FurnitureSaveManager : MonoBehaviour
{
    public static FurnitureSaveManager Instance;

    [Header("UI Buttons")]
    public Button saveButton;
    public Button loadButton;

    [Header("Status Text")]
    [Tooltip("TextMeshPro text element that shows save/load feedback")]
    public TextMeshProUGUI statusText;
    public float statusDisplayDuration = 2.5f;

    [Header("Resources Path")]
    [Tooltip("Path inside Resources/ where InventoryItemData assets live (e.g. 'Items')")]
    public string itemsResourcesPath = "Items";

    public List<GameObject> activeFurniture = new List<GameObject>();
    private string savePath;

    void Awake()
    {
        Instance = this;
        savePath = Application.persistentDataPath + "/" + SceneManager.GetActiveScene().name + "_furniture.json";
    }

    void Start()
    {
        if (saveButton != null) saveButton.onClick.AddListener(SaveGame);
        if (loadButton != null) loadButton.onClick.AddListener(LoadGame);
    }

    public void RegisterFurniture(GameObject obj)
    {
        if (obj != null && !activeFurniture.Contains(obj))
            activeFurniture.Add(obj);
    }

    public void UnregisterFurniture(GameObject obj)
    {
        activeFurniture.Remove(obj);
    }

    public void SaveGame() {
        SaveData data = new SaveData();
        activeFurniture.RemoveAll(item => item == null);

        foreach (GameObject obj in activeFurniture) {
            // Get the prefab reference from the component
            FurniturePrefabReference prefabRef = obj.GetComponent<FurniturePrefabReference>();
            string prefabName = prefabRef != null ? prefabRef.prefabPath : obj.name.Replace("(Clone)", "").Trim();
            
            FurnitureData itemData = new FurnitureData {
                prefabName = prefabName,
                position = obj.transform.position,
                rotation = obj.transform.rotation
            };
            data.allItems.Add(itemData);
            Debug.Log("Saved: " + prefabName + " at " + obj.transform.position);
        }

        string json = JsonUtility.ToJson(data, true);
        Debug.Log("JSON to save: " + json);
        
        try {
            File.WriteAllText(savePath, json);
            Debug.Log("FILE SAVED! Look here: " + savePath);
            ShowStatus($"Saved {data.allItems.Count} item{(data.allItems.Count == 1 ? "" : "s")}!");
        } catch (System.Exception e) {
            Debug.LogError("Failed to save file: " + e.Message);
            ShowStatus("Save failed!");
        }
    }

    public void LoadGame() {
        Debug.Log("Save file path: " + savePath);
        
        if (!File.Exists(savePath)) {
            Debug.LogWarning("No save file found at: " + savePath);
            ShowStatus("No save file found.");
            return;
        }

        string json = File.ReadAllText(savePath);
        Debug.Log("Raw JSON content: " + json);
        Debug.Log("JSON length: " + json.Length);
        
        if (string.IsNullOrEmpty(json)) {
            Debug.LogWarning("Save file is empty!");
            ShowStatus("Save file is empty.");
            return;
        }

        try {
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            
            if (data == null || data.allItems == null) {
                Debug.LogWarning("Failed to deserialize JSON - data is null!");
                ShowStatus("Load failed: corrupt file.");
                return;
            }
            
            if (data.allItems.Count == 0) {
                Debug.LogWarning("Save file has no furniture items!");
                ShowStatus("No furniture in save file.");
                return;
            }

            Debug.Log("Found " + data.allItems.Count + " items to load.");

            // Clear existing furniture before loading to prevent duplicates
            activeFurniture.RemoveAll(item => item == null);
            foreach (GameObject existing in activeFurniture)
                Destroy(existing);
            activeFurniture.Clear();

            foreach (FurnitureData item in data.allItems) {
                Debug.Log("Attempting to load InventoryItemData: " + item.prefabName);
                
                // Load the InventoryItemData ScriptableObject from the correct path
                InventoryItemData itemData = Resources.Load<InventoryItemData>(itemsResourcesPath + "/" + item.prefabName);

                if (itemData != null && itemData.prefab3D != null) {
                    GameObject newObj = Instantiate(itemData.prefab3D, item.position, item.rotation);
                    
                    // Add the prefab reference component
                    FurniturePrefabReference prefabRef = newObj.AddComponent<FurniturePrefabReference>();
                    prefabRef.prefabPath = item.prefabName;
                    
                    activeFurniture.Add(newObj);
                    Debug.Log("Loaded: " + item.prefabName);
                } else {
                    Debug.LogError("FAILED: Cannot find InventoryItemData at Resources/" + itemsResourcesPath + "/" + item.prefabName);
                    if (itemData != null && itemData.prefab3D == null) {
                        Debug.LogError("InventoryItemData found but prefab3D is null!");
                    }
                }
            }
            
            Debug.Log("Load complete! Total furniture loaded: " + activeFurniture.Count);
            ShowStatus($"Loaded {activeFurniture.Count} item{(activeFurniture.Count == 1 ? "" : "s")}!");
        } catch (System.Exception e) {
            Debug.LogError("Error during load: " + e.Message + "\n" + e.StackTrace);
            ShowStatus("Load failed!");
        }
    }

    private void ShowStatus(string message)
    {
        if (statusText == null) return;
        StopCoroutine(nameof(ClearStatusAfterDelay));
        statusText.text = message;
        statusText.gameObject.SetActive(true);
        StartCoroutine(nameof(ClearStatusAfterDelay));
    }

    private IEnumerator ClearStatusAfterDelay()
    {
        yield return new WaitForSeconds(statusDisplayDuration);
        if (statusText != null) statusText.gameObject.SetActive(false);
    }

    [System.Serializable]
    public class FurnitureData {
        public string prefabName;
        public Vector3 position;
        public Quaternion rotation;
    }

    [System.Serializable]
    public class SaveData {
        public List<FurnitureData> allItems = new List<FurnitureData>();
    }
}