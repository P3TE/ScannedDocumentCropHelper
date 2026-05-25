using System;
using System.IO;
using Unity.VisualScripting;
using Unity.VisualScripting.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DefaultNamespace
{
    public enum BoxCropStage
    {
        NoSelectionsMade,
        BotLeftSelected,
        TopLeftSelected,
        TopRightSelected
    }
    
    public class ImageCropHandler : MonoBehaviour
    {
        private const string InputImagePath = @"/home/p3te/Pictures/d5200/photocopy_d5200/original/2026-04-18_16-37-12_full_res.jpg";


        [SerializeField] private RectTransform inputImageContainer;
        [SerializeField] private Image inputDisplayImage;
        
        [SerializeField] private InputActionReference rotateInputCounterClockwiseActionReference;
        [SerializeField] private InputActionReference rotateInputClockwiseActionReference;
        
        [Space]
        
        [SerializeField] private InputActionReference mousePositionActionReference;
        [SerializeField] private InputActionReference mouseClickActionReference;
        
        [Space]
        
        [SerializeField] private InputActionReference resetToFirstSelectionActionReference;
        [SerializeField] private InputActionReference resetToSecondSelectionActionReference;
        [SerializeField] private InputActionReference resetToThirdSelectionActionReference;

        [Header("Box Crop Display")]
        [SerializeField] private Image botLeftPositionDisplay;
        [SerializeField] private Image lhsEdgeLineDisplay;
        [SerializeField] private Image topLeftPositionDisplay;
        [SerializeField] private Image topEdgeLineDisplay;
        [SerializeField] private Image topRightPositionDisplay;
        [SerializeField] private Image rhsEdgeLineDisplay;
        [SerializeField] private Image botEdgeLineDisplay;
        
        private Texture2D _tex;
        private Sprite _sprite;

        private int _inputRotationDegrees = 0;
        
        private Vector2 _finalDisplaySize;
        
        // Positions are in image space.
        public BoxCropStage boxCropStage = BoxCropStage.NoSelectionsMade;
        
        // Rect transform position
        private Vector2 _selectedPositionBotLeft = new Vector2(float.NaN, float.NaN);
        private Vector2 _selectedPositionTopLeft = new Vector2(float.NaN, float.NaN);
        private Vector2 _selectedPositionTopRight = new Vector2(float.NaN, float.NaN);
        
        private void Start()
        {
            LoadImage();
            
            rotateInputCounterClockwiseActionReference.action.Enable();
            rotateInputCounterClockwiseActionReference.action.performed += OnRotateInputCounterClockwise;
            
            rotateInputClockwiseActionReference.action.Enable();
            rotateInputClockwiseActionReference.action.performed += OnRotateInputClockwise;
            
            mousePositionActionReference.action.Enable();
            
            mouseClickActionReference.action.Enable();
            mouseClickActionReference.action.performed += OnMouseClick;
            
            
            resetToFirstSelectionActionReference.action.Enable();
            resetToSecondSelectionActionReference.action.Enable();
            resetToThirdSelectionActionReference.action.Enable();
            
            resetToFirstSelectionActionReference.action.performed += ResetToFirstSelection;
            resetToSecondSelectionActionReference.action.performed += ResetToSecondSelection;
            resetToThirdSelectionActionReference.action.performed += ResetToThirdSelection;
        }

        private void ResetToFirstSelection(InputAction.CallbackContext obj)
        {
            boxCropStage = BoxCropStage.NoSelectionsMade;
        }
        
        private void ResetToSecondSelection(InputAction.CallbackContext obj)
        {
            boxCropStage = BoxCropStage.BotLeftSelected;
        }
        
        private void ResetToThirdSelection(InputAction.CallbackContext obj)
        {
            boxCropStage = BoxCropStage.TopLeftSelected;
        }

        private void OnRotateInputCounterClockwise(InputAction.CallbackContext obj)
        {
            RotateInput(-90);
        }

        private void OnRotateInputClockwise(InputAction.CallbackContext obj)
        {
            RotateInput(90);
        }

        private void OnMouseClick(InputAction.CallbackContext obj)
        {
            if (boxCropStage == BoxCropStage.NoSelectionsMade)
            {
                _selectedPositionBotLeft = GetRelativeMousePositionInContainer();
                boxCropStage = BoxCropStage.BotLeftSelected;
            }
            else if (boxCropStage == BoxCropStage.BotLeftSelected)
            {
                _selectedPositionTopLeft = GetRelativeMousePositionInContainer();
                boxCropStage = BoxCropStage.TopLeftSelected;
            }
            else if (boxCropStage == BoxCropStage.TopLeftSelected)
            {
                _selectedPositionTopRight = GetRelativeMousePositionInContainer();
                boxCropStage = BoxCropStage.TopRightSelected;
            }
            
            Debug.Log($"boxCropStage = {boxCropStage}");
        }

        private void RotateInput(int amountDegrees)
        {
            _inputRotationDegrees += amountDegrees;
            _inputRotationDegrees = ((_inputRotationDegrees % 360) + 360) % 360; // Keep in range [0, 360)
            Debug.Log(_inputRotationDegrees);
        }

        private void LoadImage()
        {
            byte[] data = File.ReadAllBytes(InputImagePath);
            _tex = new Texture2D(2, 2);
            _tex.LoadImage(data);
            
            Rect textureRect = new Rect(0, 0, _tex.width, _tex.height);
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            _sprite = Sprite.Create(_tex, textureRect, pivot);
            
            inputDisplayImage.sprite = _sprite;
            inputDisplayImage.enabled = true;
        }

        private void OnDestroy()
        {
            TryDestroyGeneratedObjects();
        }

        private void Update()
        {
            FitImageToDisplayArea();
            UpdateBoxArea();
        }

        private void UpdateBoxArea()
        {
            Vector2 mousePosition = mousePositionActionReference.action.ReadValue<Vector2>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(inputImageContainer, mousePosition, null, out Vector2 localPoint);

            UpdateBoxCropDisplays();

            if (boxCropStage == BoxCropStage.NoSelectionsMade)
            {
                _selectedPositionBotLeft = GetRelativeMousePositionInContainer();
                
                UpdatePoint(botLeftPositionDisplay, _selectedPositionBotLeft);
            }
            else if (boxCropStage == BoxCropStage.BotLeftSelected)
            {
                _selectedPositionTopLeft = GetRelativeMousePositionInContainer();
                
                UpdatePoint(topLeftPositionDisplay, _selectedPositionTopLeft);
                UpdateLine(lhsEdgeLineDisplay, _selectedPositionBotLeft, _selectedPositionTopLeft);
            } 
            else if (boxCropStage == BoxCropStage.TopLeftSelected)
            {
                Vector2 desiredTopRightPosition = GetRelativeMousePositionInContainer();
                Vector2 topLeftToDesiredTopRight = desiredTopRightPosition - _selectedPositionTopLeft;
                
                Vector2 botLeftToTopLeft = _selectedPositionTopLeft - _selectedPositionBotLeft;
                Vector2 botLeftToTopLeftNormalised = botLeftToTopLeft.normalized;
                Vector2 topLeftToTopRightDirection = new Vector2(botLeftToTopLeftNormalised.y, -botLeftToTopLeftNormalised.x); // Rotate 90 degrees to get the direction from top left to top right
                
                // Find the closest point on the line defined by the _selectedPositionTopLeft and the direction topLeftToTopRightDirection.

                float length = Vector2.Dot(topLeftToDesiredTopRight, topLeftToTopRightDirection);
                
                _selectedPositionTopRight = _selectedPositionTopLeft + length * topLeftToTopRightDirection;
                
                UpdatePoint(topRightPositionDisplay, _selectedPositionTopRight);
                Vector2 topLeftToTopRight = _selectedPositionTopRight - _selectedPositionTopLeft;
                Vector2 botRightPosition = _selectedPositionBotLeft + topLeftToTopRight;
                
                UpdateLine(topEdgeLineDisplay, _selectedPositionTopLeft, _selectedPositionTopRight);
                UpdateLine(rhsEdgeLineDisplay, _selectedPositionTopRight, botRightPosition);
                UpdateLine(botEdgeLineDisplay, botRightPosition, _selectedPositionBotLeft);
            }
        }
        
        private void UpdateBoxCropDisplays()
        {
            botLeftPositionDisplay.gameObject.SetActive(boxCropStage >= BoxCropStage.NoSelectionsMade);
            topLeftPositionDisplay.gameObject.SetActive(boxCropStage >= BoxCropStage.BotLeftSelected);
            lhsEdgeLineDisplay.gameObject.SetActive(boxCropStage >= BoxCropStage.BotLeftSelected);
            topEdgeLineDisplay.gameObject.SetActive(boxCropStage >= BoxCropStage.TopLeftSelected);
            topRightPositionDisplay.gameObject.SetActive(boxCropStage >= BoxCropStage.TopLeftSelected);
            rhsEdgeLineDisplay.gameObject.SetActive(boxCropStage >= BoxCropStage.TopLeftSelected);
            botEdgeLineDisplay.gameObject.SetActive(boxCropStage >= BoxCropStage.TopLeftSelected);
        }

        private void UpdatePoint(Image pointToUpdate, Vector2 position)
        {
            pointToUpdate.rectTransform.anchoredPosition = position;
        }

        private void UpdateLine(Image lineToUpdate, Vector2 startPosition, Vector2 endPosition)
        {
            float lineLength = Vector2.Distance(startPosition, endPosition);
            lineToUpdate.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lineLength);
            
            float angleRadians = Mathf.Atan2(endPosition.y - startPosition.y, endPosition.x - startPosition.x);
            float angleDegrees = angleRadians * Mathf.Rad2Deg;
            float displayAngleDegrees = angleDegrees - 90; // Subtract 90 degrees to account for the line's default orientation
            lineToUpdate.rectTransform.rotation = Quaternion.Euler(0, 0, displayAngleDegrees);
            
            Vector2 midPoint = (startPosition + endPosition) * 0.5f;
            lineToUpdate.rectTransform.anchoredPosition = midPoint;
        }

        private Vector2 GetRelativeMousePositionInContainer()
        {
            Vector2 mousePosition = mousePositionActionReference.action.ReadValue<Vector2>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(inputImageContainer, mousePosition, null, out Vector2 localPoint);
            return localPoint;
        }

        private void UpdateBoxVisibility()
        {
            
        }

        private void FitImageToDisplayArea()
        {
            if (_tex == null)
            {
                // Texture not loaded.
                return;
            }
            
            // Set rotation.
            Quaternion rotation = Quaternion.Euler(0, 0, -_inputRotationDegrees);
            inputDisplayImage.transform.rotation = rotation;
            
            bool widthAndHeightSwapped = _inputRotationDegrees == 90 || _inputRotationDegrees == 270;
            
            Rect availableSpace = inputImageContainer.rect;
            float inputTextureAspect = (float)_tex.width / (float)_tex.height;
            if (widthAndHeightSwapped)
            {
                inputTextureAspect = 1.0f / inputTextureAspect;
            }
            
            float widthWhenFittingHeight = availableSpace.height * inputTextureAspect;
            float heightWhenFittingWidth = availableSpace.width / inputTextureAspect;

            Vector2 displaySize;
            if (widthWhenFittingHeight <= availableSpace.width)
            {
                // Fit by height
                displaySize = new Vector2(widthWhenFittingHeight, availableSpace.height);
            }
            else
            {
                // Fit by width
                displaySize = new Vector2(availableSpace.width, heightWhenFittingWidth);
            }

            if (widthAndHeightSwapped)
            {
                _finalDisplaySize = new Vector2(
                    displaySize.y,
                    displaySize.x
                );
            }
            else
            {
                _finalDisplaySize = displaySize;
            }
            
            inputDisplayImage.rectTransform.sizeDelta = _finalDisplaySize; 
        }
        
        private void TryDestroyGeneratedObjects()
        {
            if (_tex != null)
            {
                Object.Destroy(_tex);
                _tex = null;
            }
            if (_sprite != null)
            {
                Object.Destroy(_sprite);
                _sprite = null;
            }
        }
    }
}