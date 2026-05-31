using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class PopupToast : MonoBehaviour
    {
        [SerializeField] private float fadeDurationSeconds = 1.0f;
        [SerializeField] private AnimationCurve fadeAlphaCurve;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject displayContainer;
        [SerializeField] private TextMeshProUGUI displayText;
        [SerializeField] private Image backgroundImage;

        private float _fadeTimerSeconds;
        private bool _visible;

        private void Start()
        {
            SetInvisible();
        }


        public void Update()
        {
            if (_visible)
            {
                _fadeTimerSeconds += Time.unscaledDeltaTime;
                UpdateFade();
            }
        }

        private void SetInvisible()
        {
            _visible = false;
            displayContainer.SetActive(false);
        }

        public void Show(string text, Color color)
        {
            backgroundImage.color = color;
            displayText.text = text;
            _fadeTimerSeconds = 0.0f;
            _visible = true;
            displayContainer.SetActive(true);
            UpdateFade();
        }

        private void UpdateFade()
        {
            float lerpValue = Mathf.Clamp01(_fadeTimerSeconds / fadeDurationSeconds);
            float alpha = fadeAlphaCurve.Evaluate(lerpValue);
            canvasGroup.alpha = alpha;

            if (lerpValue >= 1)
            {
                SetInvisible();
            }
        }
        
    }
}