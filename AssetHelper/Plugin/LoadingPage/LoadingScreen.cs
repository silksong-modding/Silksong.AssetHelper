using UnityEngine;
using UnityEngine.UI;

namespace Silksong.AssetHelper.Plugin.LoadingPage;

internal class LoadingScreen : MonoBehaviour, ILoadingScreen
{
    private RectTransform _fillImageRect;
    private GameObject _canvasObject;
    private CanvasGroup _canvasGroup;
    private Text _statusText;
    private Text _subText;

    void Awake()
    {
        _canvasObject = new("LoadingCanvas");
        Canvas canvas = _canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        _canvasObject.AddComponent<CanvasScaler>();
        _canvasObject.AddComponent<GraphicRaycaster>();
        _canvasGroup = _canvasObject.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = true;

        GameObject bgObj = new("Background");
        bgObj.transform.SetParent(_canvasObject.transform);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = Color.black;

        RectTransform bgRect = bgImage.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        GameObject borderObj = new("Outline");
        borderObj.transform.SetParent(_canvasObject.transform);
        Image borderImage = borderObj.AddComponent<Image>();
        borderImage.color = Color.white;

        RectTransform borderRect = borderImage.rectTransform;
        borderRect.anchorMin = new Vector2(0.5f, 0.5f);
        borderRect.anchorMax = new Vector2(0.5f, 0.5f);
        borderRect.pivot = new Vector2(0.5f, 0.5f);
        borderRect.sizeDelta = new Vector2(500, 66);
        borderRect.anchoredPosition = Vector2.zero;

        GameObject innerBgObj = new("InnerBG");
        innerBgObj.transform.SetParent(borderObj.transform);
        Image innerBgImage = innerBgObj.AddComponent<Image>();
        innerBgImage.color = Color.black;

        RectTransform innerBgRect = innerBgImage.rectTransform;
        innerBgRect.anchorMin = Vector2.zero;
        innerBgRect.anchorMax = Vector2.one;
        innerBgRect.sizeDelta = new Vector2(-4, -4);
        innerBgRect.anchoredPosition = Vector2.zero;

        GameObject fillObj = new("Fill");
        fillObj.transform.SetParent(innerBgObj.transform);
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = Color.white;

        _fillImageRect = fillImage.rectTransform;
        _fillImageRect.anchorMin = new Vector2(0, 0);
        _fillImageRect.anchorMax = new Vector2(0, 1);
        _fillImageRect.pivot = new Vector2(0, 0.5f);
        _fillImageRect.sizeDelta = Vector2.zero;
        _fillImageRect.anchoredPosition = Vector2.zero;

        {
            GameObject textObj = new("StatusText");
            textObj.transform.SetParent(_canvasObject.transform);
            _statusText = textObj.AddComponent<Text>();

            _statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _statusText.text = string.Empty;
            _statusText.fontSize = 30;
            _statusText.alignment = TextAnchor.MiddleCenter;
            _statusText.color = Color.white;

            RectTransform textRect = _statusText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.sizeDelta = new Vector2(800, 50);
            textRect.anchoredPosition = new Vector2(0, 60);
        }

        {
            GameObject subtextObj = new("SubText");
            subtextObj.transform.SetParent(_canvasObject.transform);
            _subText = subtextObj.AddComponent<Text>();

            _subText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _subText.text = string.Empty;
            _subText.fontSize = 20;
            _subText.alignment = TextAnchor.MiddleCenter;
            _subText.color = Color.white;

            RectTransform subtextRect = _subText.rectTransform;
            subtextRect.anchorMin = new Vector2(0.5f, 0.5f);
            subtextRect.anchorMax = new Vector2(0.5f, 0.5f);
            subtextRect.pivot = new Vector2(0.5f, 1f);
            subtextRect.sizeDelta = new Vector2(800, 50);
            subtextRect.anchoredPosition = new Vector2(0, -45);
        }

#if DEBUG
        {
            GameObject memTextObj = new("MemText");
            memTextObj.transform.SetParent(_canvasObject.transform);
            Text _memText = memTextObj.AddComponent<Text>();

            _memText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _memText.text = string.Empty;
            _memText.fontSize = 20;
            _memText.alignment = TextAnchor.MiddleCenter;
            _memText.color = Color.white;

            RectTransform memtextRect = _memText.rectTransform;
            memtextRect.anchorMin = new Vector2(0.5f, 0.5f);
            memtextRect.anchorMax = new Vector2(0.5f, 0.5f);
            memtextRect.pivot = new Vector2(0.5f, 1f);
            memtextRect.sizeDelta = new Vector2(800, 100);
            memtextRect.anchoredPosition = new Vector2(0, -90);

            memTextObj.AddComponent<MemoryWatcher>();
        }
#endif

        // For testing
        _statusText.text = "Loading...";
    }

    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        _fillImageRect.anchorMax = new Vector2(progress, 1f);
    }

    public void SetVisible(bool visible)
    {
        _canvasGroup.alpha = visible ? 1f : 0f;
    }

    public void SetText(string text)
    {
        _statusText.text = text;
    }

    public void SetSubtext(string text)
    {
        _subText.text = text;
    }

    private void OnDestroy()
    {
        if (_canvasObject != null)
        {
            Destroy(_canvasObject);
        }
    }
}
