using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RopeTypeUIController : MonoBehaviour
{
    [Header("References")]
    public RigRopeController ropeController;
    public TMP_Dropdown ropeTypeDropdown;
    public TextMeshProUGUI ropeInfoText;
    public Image ropeColorPreview;

    [Header("Preview Colors")]
    public Color cottonPreviewColor = new Color(0.75f, 0.52f, 0.32f);
    public Color nylonPreviewColor = new Color(0.1f, 0.25f, 0.75f);
    public Color steelPreviewColor = new Color(0.55f, 0.55f, 0.58f);

    void Start()
    {
        SetupDropdown();
        SyncUIWithCurrentRopeType();
    }

    void OnEnable()
    {
        if (ropeTypeDropdown != null)
        {
            ropeTypeDropdown.onValueChanged.AddListener(OnRopeTypeChanged);
        }
    }

    void OnDisable()
    {
        if (ropeTypeDropdown != null)
        {
            ropeTypeDropdown.onValueChanged.RemoveListener(OnRopeTypeChanged);
        }
    }

    void SetupDropdown()
    {
        if (ropeTypeDropdown == null)
        {
            return;
        }

        ropeTypeDropdown.ClearOptions();

        ropeTypeDropdown.AddOptions(new List<string>
        {
            "Cotton Rope",
            "Nylon Rope",
            "Steel Rope"
        });
    }

    void SyncUIWithCurrentRopeType()
    {
        if (ropeController == null || ropeTypeDropdown == null)
        {
            return;
        }

        int currentIndex = (int)ropeController.ropeType;

        ropeTypeDropdown.SetValueWithoutNotify(currentIndex);
        UpdateInfoPanel(currentIndex);
    }

    public void OnRopeTypeChanged(int index)
    {
        if (ropeController == null)
        {
            return;
        }

        ropeController.SetRopeTypeByIndex(index);
        UpdateInfoPanel(index);
    }

    void UpdateInfoPanel(int index)
    {
        string description = "";
        Color previewColor = cottonPreviewColor;

        if (index == 0)
        {
            description = "Cotton: fabric rope, slightly flexible with realistic tiny sway.";
            previewColor = cottonPreviewColor;
        }
        else if (index == 1)
        {
            description = "Nylon: strong synthetic rope, mostly taut with controlled elasticity.";
            previewColor = nylonPreviewColor;
        }
        else if (index == 2)
        {
            description = "Steel: near-rigid cable, minimal sag, high tension response.";
            previewColor = steelPreviewColor;
        }

        if (ropeInfoText != null)
        {
            ropeInfoText.text = description;
        }

        if (ropeColorPreview != null)
        {
            ropeColorPreview.color = previewColor;
        }
    }
}