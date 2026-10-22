using TMPro;
using Unity.Netcode;
using UnityEngine;

public class NetworkCoopGoal : NetworkBehaviour
{
    [SerializeField]
    private int targetCoinCount = 3;

    public NetworkVariable<int> CollectedCoins =
        new(0);

    public NetworkVariable<bool> GameSucceeded =
        new(false);

    private GameObject runtimeUiObject;
    private TextMeshProUGUI goalText;

    public override void OnNetworkSpawn()
    {
        CollectedCoins.OnValueChanged +=
            HandleCollectedCoinsChanged;

        GameSucceeded.OnValueChanged +=
            HandleGameSucceededChanged;

        CreateRuntimeUi();
        UpdateGoalUi();
    }

    public override void OnNetworkDespawn()
    {
        CollectedCoins.OnValueChanged -=
            HandleCollectedCoinsChanged;

        GameSucceeded.OnValueChanged -=
            HandleGameSucceededChanged;

        if (runtimeUiObject != null)
            Destroy(runtimeUiObject);
    }

    public void RegisterCoinCollectedServer()
    {
        if (!IsServer)
            return;

        CollectedCoins.Value++;

        Debug.Log(
            $"DAY15 SharedGoalProgress | " +
            $"Collected={CollectedCoins.Value} | " +
            $"Target={targetCoinCount}");

        if (GameSucceeded.Value ||
            CollectedCoins.Value < targetCoinCount)
        {
            return;
        }

        GameSucceeded.Value = true;

        Debug.Log(
            $"DAY15 GameSuccess | " +
            $"Collected={CollectedCoins.Value} | " +
            $"Target={targetCoinCount} | " +
            $"ExecutedOnce=True");
    }

    private void HandleCollectedCoinsChanged(
        int previousValue,
        int newValue)
    {
        UpdateGoalUi();
    }

    private void HandleGameSucceededChanged(
        bool previousValue,
        bool newValue)
    {
        UpdateGoalUi();
    }

    private void CreateRuntimeUi()
    {
        if (runtimeUiObject != null)
            return;

        Canvas targetCanvas = null;

        Canvas[] canvases =
            FindObjectsByType<Canvas>(
                FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            if (canvas != null &&
                canvas.renderMode ==
                    RenderMode.ScreenSpaceOverlay)
            {
                targetCanvas = canvas;
                break;
            }
        }

        if (targetCanvas == null)
        {
            Debug.LogWarning(
                "DAY15 SharedGoal UI 생성 실패: Screen Space Overlay Canvas 없음");

            return;
        }

        TextMeshProUGUI template =
            targetCanvas.GetComponentInChildren<TextMeshProUGUI>(
                true);

        runtimeUiObject =
            new GameObject(
                "SharedGoalText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        runtimeUiObject.transform.SetParent(
            targetCanvas.transform,
            false);

        RectTransform rect =
            runtimeUiObject.GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0.5f, 1f);

        rect.anchorMax =
            new Vector2(0.5f, 1f);

        rect.pivot =
            new Vector2(0.5f, 1f);

        rect.anchoredPosition =
            new Vector2(0f, -20f);

        rect.sizeDelta =
            new Vector2(600f, 90f);

        goalText =
            runtimeUiObject.GetComponent<TextMeshProUGUI>();

        if (template != null &&
            template.font != null)
        {
            goalText.font =
                template.font;
        }

        goalText.fontSize = 32f;
        goalText.alignment =
            TextAlignmentOptions.Top;

        goalText.raycastTarget = false;
    }

    private void UpdateGoalUi()
    {
        if (goalText == null)
            return;

        if (GameSucceeded.Value)
        {
            goalText.text =
                $"게임 성공\n공동 코인: {CollectedCoins.Value} / {targetCoinCount}";

            return;
        }

        goalText.text =
            $"공동 목표: 코인 {CollectedCoins.Value} / {targetCoinCount}";
    }
}
