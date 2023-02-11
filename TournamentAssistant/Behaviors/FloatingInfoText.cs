using BeatSaberMarkupLanguage;
using TMPro;
using TournamentAssistant.Misc;
using UnityEngine;

namespace TournamentAssistant.Behaviors
{
    class FloatingInfoText : MonoBehaviour
    {
        public static FloatingInfoText Instance { get; set; }

        private TextMeshProUGUI _textMesh;
        private string _hiddenText;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);

            gameObject.transform.position = new Vector3(0, 0.001f, 3f);
            gameObject.transform.eulerAngles = new Vector3(90, 0, 0);
            gameObject.transform.localScale = new Vector3(0.05f, 0.05f, 0.0f);

            var mainCanvas = gameObject.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.WorldSpace;

            _textMesh = BeatSaberUI.CreateText(transform as RectTransform, "", new Vector2(.5f, 0));
            _textMesh.fontSize = 8f;
            _textMesh.lineSpacing = -40f;
            _textMesh.alignment = TextAlignmentOptions.Center;
        }

        public void UpdateText(string newText)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => _textMesh.SetText(newText));
        }

        public void HideText()
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _hiddenText = _textMesh.text;
                _textMesh.SetText("");
            });
        }

        public void UnhideText()
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _textMesh.SetText(_hiddenText);
                _hiddenText = "";
            });
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            Destroy(_textMesh);
            Instance = null;
        }
    }
}
