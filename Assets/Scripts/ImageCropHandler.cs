using System;
using System.Collections;
using System.IO;
using System.Text;
using CompactExifLib;
using TMPro;
using Unity.VisualScripting;
using Unity.VisualScripting.InputSystem;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
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
        // private const string InputImagePath = @"/home/p3te/Pictures/d5200/photocopy_d5200/original/2026-04-18_16-37-12_full_res.jpg";
        // private const string OutputImagePath = @"/home/p3te/Pictures/d5200/photocopy_d5200/cropped_and_rotated/test_output_1.jpg";

        private InputOutputImagesHelper _inputOutputImagesHelper = new(
            "/home/p3te/Pictures/d5200/photocopy_d5200/original/",
            "/home/p3te/Pictures/d5200/photocopy_d5200/cropped_and_rotated/"
        );

        [SerializeField] private RectTransform inputImageContainer;
        [SerializeField] private Image inputDisplayImage;
        
        [Space]
        
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TMP_InputField qualityInputField;
        [SerializeField] private PopupToast toast;
        [SerializeField] private Color goodToastColor;
        
        [Space]
        
        [SerializeField] private InputActionReference rotateInputCounterClockwiseActionReference;
        [SerializeField] private InputActionReference rotateInputClockwiseActionReference;
        
        [Space]
        
        [SerializeField] private InputActionReference mousePositionActionReference;
        [SerializeField] private InputActionReference mouseClickActionReference;
        
        [Space]
        
        [SerializeField] private InputActionReference resetToFirstSelectionActionReference;
        [SerializeField] private InputActionReference resetToSecondSelectionActionReference;
        [SerializeField] private InputActionReference resetToThirdSelectionActionReference;
        
        [Space]
        
        [SerializeField] private InputActionReference saveOutputFileActionReference;

        [Header("Box Crop Display")]
        [SerializeField] private Image botLeftPositionDisplay;
        [SerializeField] private Image lhsEdgeLineDisplay;
        [SerializeField] private Image topLeftPositionDisplay;
        [SerializeField] private Image topEdgeLineDisplay;
        [SerializeField] private Image topRightPositionDisplay;
        [SerializeField] private Image rhsEdgeLineDisplay;
        [SerializeField] private Image botEdgeLineDisplay;
        
        [Header("RHS_Cropped_Result")]
        
        [SerializeField] private RectTransform outputImageContainer;
        [SerializeField] private RectTransform outputImageMask;
        [SerializeField] private Image outputCroppedDisplayImage;
        
        [SerializeField] private RawImage outputRawImage;
        
        [Header("Output Image")]
        
        [SerializeField] private Camera outputImageCamera;
        [SerializeField] private Canvas outputImageCanvas;
        [SerializeField] private Image fullOutputImage;
        
        private bool _outputTextureIsValidForDisplay = false;
        
        private Texture2D _tex;
        private Sprite _sprite;
        private Sprite _outputSprite;
        private Sprite _outputRenderTextureSprite;
        private RenderTexture _outputTexture;

        private int _inputRotationDegrees = 0;

        private InputOutputImageFilePair _currentImagePaths;
        
        private Vector2 _finalDisplaySize;
        
        // Positions are in image space.
        public BoxCropStage boxCropStage = BoxCropStage.NoSelectionsMade;
        
        // Rect transform position
        private Vector2 _selectedPositionBotLeft = new Vector2(float.NaN, float.NaN);
        private Vector2 _selectedPositionTopLeft = new Vector2(float.NaN, float.NaN);
        private Vector2 _selectedPositionTopRight = new Vector2(float.NaN, float.NaN);

        private int renderTexNum = 0;
        
        // Texture space selected position (origin at bottom left of texture, in pixels).
        // private Vector2 _inputTextureSelectedBotLeft;
        // private Vector2 _inputTextureSelectedTopLeft;
        // private Vector2 _inputTextureSelectedTopRight;

        private SelectionPositions _previousTextureSelectedPositions = new();
        private SelectionPositions _inputTextureSelectedPositions = new();

        private ExifData _inputExifDataCopy;
        
        private int _outputJpegQuality = 100;

        private class SelectionPositions : IEquatable<SelectionPositions>
        {
            public Vector2 BotLeft;
            public Vector2 TopLeft;
            public Vector2 TopRight;

            public void SetValues(SelectionPositions other)
            {
                this.BotLeft = other.BotLeft;
                this.TopLeft = other.TopLeft;
                this.TopRight = other.TopRight;
            }

            public bool Equals(SelectionPositions other)
            {
                return BotLeft.Equals(other.BotLeft) && TopLeft.Equals(other.TopLeft) && TopRight.Equals(other.TopRight);
            }

            public override bool Equals(object obj)
            {
                return obj is SelectionPositions other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(BotLeft, TopLeft, TopRight);
            }
        }
        
        private void Start()
        {
            _inputOutputImagesHelper.FindExistingFiles();
            UpdateProgressText();
            
            LoadNextImage();
            
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
            
            saveOutputFileActionReference.action.Enable();
            saveOutputFileActionReference.action.performed += SaveOutputFile;
            
            qualityInputField.onEndEdit.AddListener(OnQualityInputFieldEndEdit);
            ValidateOutputQualitySetting();
        }

        private void UpdateProgressText()
        {
            StringBuilder progressTextBuilder = new();
            progressTextBuilder.Append(_inputOutputImagesHelper.GetExistingOutputFileCount());
            progressTextBuilder.Append("/");
            progressTextBuilder.Append(_inputOutputImagesHelper.TotalFileCount);
            progressText.text = progressTextBuilder.ToString();
        }
        
        private void OnQualityInputFieldEndEdit(string value)
        {
            ValidateOutputQualitySetting();
        }

        private void ValidateOutputQualitySetting()
        {
            string qualityText = qualityInputField.text;
            
            if (int.TryParse(qualityText, out int qualityValue))
            {
                _outputJpegQuality = Math.Clamp(qualityValue, 0, 100);
            }
            
            qualityInputField.text = _outputJpegQuality.ToString();
        }

        private void ResetToFirstSelection(InputAction.CallbackContext obj)
        {
            TrySetBoxCropStage(BoxCropStage.NoSelectionsMade);
        }
        
        private void ResetToSecondSelection(InputAction.CallbackContext obj)
        {
            TrySetBoxCropStage(BoxCropStage.BotLeftSelected);
        }
        
        private void ResetToThirdSelection(InputAction.CallbackContext obj)
        {
            TrySetBoxCropStage(BoxCropStage.TopLeftSelected);
        }
        
        private void TrySetBoxCropStage(BoxCropStage desiredStage)
        {
            if (qualityInputField.isFocused) return;
            
            if (boxCropStage < desiredStage)
            {
                return;
            }
            
            boxCropStage = desiredStage;
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
                SetTopRightPositionBasedOnMouse(GetRelativeMousePositionInContainer());
                boxCropStage = BoxCropStage.TopRightSelected;
            }
            
            Debug.Log($"boxCropStage = {boxCropStage}");
        }
        
        private void SaveOutputFile(InputAction.CallbackContext obj)
        {
            TrySaveFile();
        }

        private void TrySaveFile()
        {
            StartCoroutine(SaveOutputFileDelayed());
        }

        private IEnumerator SaveOutputFileDelayed()
        {
            toast.Show("Saving file...", goodToastColor);
            yield return null;
            try
            {
                PerformFileSave();
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to save file: " + e);
                toast.Show($"Save failed! {e.Message}", Color.red);
            }
        }

        private void PerformFileSave()
        {
            Debug.Log("Save output file requested.");

            if (boxCropStage != BoxCropStage.TopRightSelected)
            {
                throw new Exception("Cannot save output file because the crop box is not fully defined.");
            }

            if (!_outputTextureIsValidForDisplay)
            {
                throw new Exception("Cannot save output file because the output texture is not valid for display.");
            }

            if (_outputTexture == null)
            {
                throw new Exception($"{nameof(_outputTexture)} is null!");
            }
            
            // _outputTexture.
            Texture2D texture2D = new Texture2D(_outputTexture.width, _outputTexture.height, TextureFormat.ARGB32, false);
            RenderTexture.active = _outputTexture;
            texture2D.ReadPixels(new Rect(0, 0, _outputTexture.width, _outputTexture.height), 0, 0);
            texture2D.Apply();

            ValidateOutputQualitySetting();
            byte[] jpegBytes = texture2D.EncodeToJPG(_outputJpegQuality);

            FileStream fileStream = File.OpenWrite(_currentImagePaths.OutputFilePath);
            fileStream.Write(jpegBytes, 0, jpegBytes.Length);
            fileStream.Flush();
            fileStream.Close();

            string outputFileNoExif = _currentImagePaths.OutputFilePath;
            ExifData outputExifData = new ExifData(outputFileNoExif);
            outputExifData.ReplaceAllTagsBy(_inputExifDataCopy);
            
            // Set the orientation to 0.
            outputExifData.SetTagValue(ExifTag.Orientation, 0, ExifTagType.UShort);
            
            MemoryStream noExifDataStream = new MemoryStream(jpegBytes);
            noExifDataStream.Seek(0, SeekOrigin.Begin);
            Stream outputFileStream = File.OpenWrite(_currentImagePaths.OutputFilePath);
            outputExifData.Save(noExifDataStream, outputFileStream);
            
            _currentImagePaths.OnOutputFileCreated();
            UpdateProgressText();
            
            Debug.Log("File saving complete.");
            
            LoadNextImage();
        }

        private void RotateInput(int amountDegrees)
        {
            SetRotationOfInputImage(_inputRotationDegrees + amountDegrees);
        }

        private void SetRotationOfInputImage(int rotationDegrees)
        {
            _inputRotationDegrees = ((rotationDegrees % 360) + 360) % 360; // Keep in range [0, 360)
        }

        private void LoadNextImage()
        {
            for(int i = 0; i < _inputOutputImagesHelper.TotalFileCount; i++)
            {
                // Find the first image that doesn't have an output file yet.
                InputOutputImageFilePair filePair = _inputOutputImagesHelper[i];
                
                if (!filePair.OutputFileExists)
                {
                    _currentImagePaths = filePair;
                    LoadImage();
                    return;
                }
            }
            
            Debug.Log("All images cropped and rotated!");
        }

        private void LoadImage()
        {
            TrySetBoxCropStage(BoxCropStage.NoSelectionsMade);
            
            string fileName = Path.GetFileName(_currentImagePaths.InputFilePath);

            Debug.Log("Loading exif data...");
            ExifData inputExifData = new(_currentImagePaths.InputFilePath);
            // Copy the original ExifData into a new ExifData instance.
            _inputExifDataCopy = ExifData.Empty();
            _inputExifDataCopy.ReplaceAllTagsBy(inputExifData);

            TrySetInitialOrientation(_inputExifDataCopy);
            // PrintExifData(_inputExifDataCopy);
            
            byte[] data = File.ReadAllBytes(_currentImagePaths.InputFilePath);
            TryDestroyObject(ref _tex);
            _tex = new Texture2D(2, 2);
            _tex.LoadImage(data);
            _tex.filterMode = FilterMode.Trilinear;
            
            LoadSprite(_tex, ref _sprite, fileName, inputDisplayImage);
            LoadSprite(_tex, ref _outputSprite, fileName, outputCroppedDisplayImage);
            LoadSprite(_tex, ref _outputRenderTextureSprite, fileName, fullOutputImage);
            
            // Always display the full output image at the texture's native resolution.
            fullOutputImage.rectTransform.sizeDelta = new Vector2(_tex.width, _tex.height); 
        }

        private void TrySetInitialOrientation(ExifData exifData)
        {
            exifData.InitTagEnumeration(ExifIfd.PrimaryData);
            
            while (exifData.EnumerateNextTag(out ExifTag exifTag))
            {
                if (exifTag != ExifTag.Orientation) continue;
                
                if (!exifData.GetTagType(exifTag, out ExifTagType tagType)) continue;

                if (tagType != ExifTagType.UShort)
                {
                    Debug.LogWarning($"{nameof(ExifTag.Orientation)} tag has unexpected tag type: " + tagType);
                    return;
                }
                
                if (!exifData.GetTagValueCount(exifTag, out int valueCount)) continue;

                if (valueCount != 1)
                {
                    Debug.LogWarning("Expected value count of 1 for " + nameof(ExifTag.Orientation) + " tag, but got: " + valueCount);
                }
                
                exifData.GetTagValue(exifTag, out uint value, 0);
                Debug.Log($"Orientation tag value = {value}");

                if (value == 8)
                {
                    SetRotationOfInputImage(-90);
                }
                else if (value == 0)
                {
                    SetRotationOfInputImage(0);
                }
            }
        }

        private void PrintExifData(ExifData exifData)
        {
            foreach (ExifIfd ifd in Enum.GetValues(typeof(ExifIfd)))
            {
                Debug.Log($"Checking ifd: {ifd}");
                exifData.InitTagEnumeration(ifd);
                
                while (exifData.EnumerateNextTag(out ExifTag exifTag))
                {
                    // exifTag.
                    Debug.Log($"Found exif tag: {exifTag}");

                    exifData.GetTagType(exifTag, out ExifTagType tagType);
                    exifData.GetTagValueCount(exifTag, out int valueCount);

                    Debug.Log($"It has tagType: {tagType} with a value count of: {valueCount}");

                    if (tagType == ExifTagType.Ascii)
                    {
                        bool success = exifData.GetTagRawData(exifTag, out ExifTagType tagType2, out int valueCount2,
                            out byte[] rawData);

                        if (!success)
                        {
                            Debug.Log($"Failed to get raw data for tag {exifTag}");
                        }
                        else
                        {
                            string rawDataAsString = System.Text.Encoding.UTF8.GetString(rawData);
                            Debug.Log(
                                $"Raw data tagType2 = {tagType2}, valueCount2 = {valueCount2} as string: {rawDataAsString}");
                        }
                    }
                    else if (tagType == ExifTagType.URational)
                    {
                        for (int i = 0; i < valueCount; i++)
                        {
                            exifData.GetTagValue(exifTag, out ExifRational value, 0);
                            Debug.Log($"Value {i} = Sign = {value.Sign}, Numer = {value.Numer}, Denom = {value.Denom}");
                        }
                    } else if (tagType == ExifTagType.UShort)
                    {
                        for (int i = 0; i < valueCount; i++)
                        {
                            exifData.GetTagValue(exifTag, out uint value, i);
                            Debug.Log($"Value {i} = value = {value}");
                        }
                    }
                }
            }
        }

        private static void LoadSprite(Texture2D tex, ref Sprite sprite, string imageName, Image displayImage)
        {
            TryDestroyObject(ref sprite);
            Rect textureRect = new Rect(0, 0, tex.width, tex.height);
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            sprite = Sprite.Create(tex, textureRect, pivot);
            sprite.name = imageName;
            // displayImage.name = imageName; // Changes the game object name, not what I wanted.
            displayImage.sprite = sprite;
            displayImage.enabled = true;
        }

        private void OnDestroy()
        {
            TryDestroyGeneratedObjects();
        }

        private void Update()
        {
            FitImageToDisplayArea();
            UpdateBoxArea();
            UpdateImageCoords();
            UpdateRhsDisplay();
        }

        private void UpdateRhsDisplay()
        {
            outputRawImage.enabled = _outputTextureIsValidForDisplay;
            outputRawImage.gameObject.SetActive(_outputTextureIsValidForDisplay);

            if (_outputTextureIsValidForDisplay)
            {
                Vector2 outputRenderTextureResolution = new Vector2(_outputTexture.width, _outputTexture.height);
                Vector2 availableSpace = outputImageContainer.rect.size;
                float outputTextureAspect = outputRenderTextureResolution.x / outputRenderTextureResolution.y;
                
                float desiredHeightUsingAvailableWidth = availableSpace.x / outputTextureAspect;
                float desiredWidthUsingAvailableHeight = availableSpace.y * outputTextureAspect;

                float overshootWidthUsingHeight = desiredWidthUsingAvailableHeight - availableSpace.x;
                float overshootHeightUsingWidth = desiredHeightUsingAvailableWidth - availableSpace.y;

                Vector2 finalOutputImageSize;
                if (overshootWidthUsingHeight > overshootHeightUsingWidth)
                {
                    finalOutputImageSize = new Vector2(
                        availableSpace.x,
                        desiredHeightUsingAvailableWidth
                    );
                }
                else
                {
                    finalOutputImageSize = new Vector2(
                        desiredWidthUsingAvailableHeight,
                        availableSpace.y
                    );
                }
                
                outputRawImage.rectTransform.sizeDelta = finalOutputImageSize;
                outputRawImage.rectTransform.anchoredPosition = Vector2.zero;
                
                // fullOutputImage
                Vector2 textureBotLeftToTopLeft = _inputTextureSelectedPositions.TopLeft - _inputTextureSelectedPositions.BotLeft;
                Vector2 textureTopLeftToTopRight = _inputTextureSelectedPositions.TopRight - _inputTextureSelectedPositions.TopLeft;
                
                float outputImageRotationRadians = Mathf.Atan2(textureTopLeftToTopRight.y, textureTopLeftToTopRight.x);
                float outputImageRotationDegrees = outputImageRotationRadians * Mathf.Rad2Deg;
                Quaternion outputImageRotation = Quaternion.Euler(0, 0, -outputImageRotationDegrees);
                fullOutputImage.rectTransform.rotation = outputImageRotation;

                // Vector2 outputImageSize = fullOutputImage.rectTransform.sizeDelta;
                // fullOutputImage.rectTransform.sizeDelta = outputImageSize;
            
                // Calculate the pivot
                Vector2 cropCenterInTextureSpace = _inputTextureSelectedPositions.BotLeft + (textureBotLeftToTopLeft * 0.5f) + (textureTopLeftToTopRight * 0.5f);
                Vector2 cropCenterOffsetFromTextureCenter = cropCenterInTextureSpace - new Vector2(_tex.width * 0.5f, _tex.height * 0.5f);
                Vector2 cropCenterOffsetFromTextureCenterInOutputImage = cropCenterOffsetFromTextureCenter;
            
                Vector2 outputImageAnchoredPosition = outputImageRotation * -cropCenterOffsetFromTextureCenterInOutputImage;
                fullOutputImage.rectTransform.anchoredPosition = outputImageAnchoredPosition;
            }
        }

        private void UpdateImageCoords()
        {
            if (!CalculateOutputBox())
            {
                outputImageMask.gameObject.SetActive(false);
                _outputTextureIsValidForDisplay = false;
                return;
            }
            
            outputImageMask.gameObject.SetActive(true);
            UpdateOutputImage();
            _outputTextureIsValidForDisplay = UpdateOutputRenderTexture();
        }

        private bool UpdateOutputRenderTexture()
        {
            if (_previousTextureSelectedPositions.Equals(_inputTextureSelectedPositions))
            {
                // Nothing to change.
                return _outputTextureIsValidForDisplay;
            }
            
            // Update previous values.
            _previousTextureSelectedPositions.SetValues(_inputTextureSelectedPositions);
            
            TryDestroyOutputRenderTexture();
            
            Vector2 textureBotLeftToTopLeft = _inputTextureSelectedPositions.TopLeft - _inputTextureSelectedPositions.BotLeft;
            Vector2 textureTopLeftToTopRight = _inputTextureSelectedPositions.TopRight - _inputTextureSelectedPositions.TopLeft;
            
            int cropWidthPixels = Mathf.RoundToInt(textureTopLeftToTopRight.magnitude);
            int cropHeightPixels = Mathf.RoundToInt(textureBotLeftToTopLeft.magnitude);

            const int minRenderSizePixels = 32;
            if (cropWidthPixels < minRenderSizePixels || cropHeightPixels < minRenderSizePixels)
            {
                // Avoid creating a render texture that is too small, which can cause issues.
                return false;
            }

            _outputTexture = new RenderTexture(cropWidthPixels, cropHeightPixels, 32, RenderTextureFormat.ARGB32);
            renderTexNum++;
            _outputTexture.name = $"({cropWidthPixels}x{cropHeightPixels}) Output {renderTexNum}";
            
            // Make sure that the Render Mode is ScreenSpaceCamera, ScreenSpaceOverlay doesn't work with Render Texture for whatever reason.
            outputImageCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            outputImageCanvas.worldCamera = outputImageCamera;
            
            outputImageCamera.targetTexture = _outputTexture;
            outputRawImage.texture = _outputTexture;

            return true;
        }

        private void UpdateOutputImage()
        {
            // private Vector2 _inputTextureSelectedPositions.BotLeft;
            // private Vector2 _inputTextureSelectedPositions.TopLeft;
            // private Vector2 _inputTextureSelectedPositions.TopRight;
            
            Vector2 textureBotLeftToTopLeft = _inputTextureSelectedPositions.TopLeft - _inputTextureSelectedPositions.BotLeft;
            Vector2 textureTopLeftToTopRight = _inputTextureSelectedPositions.TopRight - _inputTextureSelectedPositions.TopLeft;
            
            float cropWidthPixels = textureTopLeftToTopRight.magnitude;
            float cropHeightPixels = textureBotLeftToTopLeft.magnitude;
            
            Vector2 cropSize = new Vector2(cropWidthPixels, cropHeightPixels);
            Vector2 displaySizePixels = FitOutputImageToDisplayArea(cropSize);

            float outputImageRotationRadians = Mathf.Atan2(textureTopLeftToTopRight.y, textureTopLeftToTopRight.x);
            float outputImageRotationDegrees = outputImageRotationRadians * Mathf.Rad2Deg;
            Quaternion outputImageRotation = Quaternion.Euler(0, 0, -outputImageRotationDegrees);
            outputCroppedDisplayImage.rectTransform.rotation = outputImageRotation;

            float ratioOfDisplaySizeToCropSize = displaySizePixels.x / cropWidthPixels;
            Vector2 textureSize = new Vector2(_tex.width, _tex.height);
            Vector2 outputImageSize = textureSize * ratioOfDisplaySizeToCropSize;
            outputCroppedDisplayImage.rectTransform.sizeDelta = outputImageSize;
            
            // Calculate the pivot
            Vector2 cropCenterInTextureSpace = _inputTextureSelectedPositions.BotLeft + (textureBotLeftToTopLeft * 0.5f) + (textureTopLeftToTopRight * 0.5f);
            Vector2 cropCenterOffsetFromTextureCenter = cropCenterInTextureSpace - new Vector2(_tex.width * 0.5f, _tex.height * 0.5f);
            Vector2 cropCenterOffsetFromTextureCenterInOutputImage = cropCenterOffsetFromTextureCenter * ratioOfDisplaySizeToCropSize;
            
            Vector2 outputImageAnchoredPosition = outputImageRotation * -cropCenterOffsetFromTextureCenterInOutputImage;
            outputCroppedDisplayImage.rectTransform.anchoredPosition = outputImageAnchoredPosition;
        }
        
        private Vector2 FitOutputImageToDisplayArea(Vector2 cropSize)
        {
            Rect availableSpace = outputImageContainer.rect;
            float outputTextureAspect = cropSize.x / cropSize.y;
            
            float widthWhenFittingHeight = availableSpace.height * outputTextureAspect;
            float heightWhenFittingWidth = availableSpace.width / outputTextureAspect;

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
            
            outputImageMask.sizeDelta = displaySize;
            return displaySize;
        }

        private bool CalculateOutputBox()
        {
            if (boxCropStage <= BoxCropStage.BotLeftSelected) return false;
            
            if (!float.IsFinite(_selectedPositionBotLeft.x)) return false;
            if (!float.IsFinite(_selectedPositionTopLeft.x)) return false;
            if (!float.IsFinite(_selectedPositionTopRight.x)) return false;

            _inputTextureSelectedPositions.BotLeft = ToInputTexturePosition(_selectedPositionBotLeft);
            _inputTextureSelectedPositions.TopLeft = ToInputTexturePosition(_selectedPositionTopLeft);
            _inputTextureSelectedPositions.TopRight = ToInputTexturePosition(_selectedPositionTopRight);
            
            Vector2 textureBotLeftToTopLeft = _inputTextureSelectedPositions.TopLeft - _inputTextureSelectedPositions.BotLeft;
            Vector2 textureTopLeftToTopRight = _inputTextureSelectedPositions.TopRight - _inputTextureSelectedPositions.TopLeft;
            
            float cropWidthPixels = textureTopLeftToTopRight.magnitude;
            float cropHeightPixels = textureBotLeftToTopLeft.magnitude;

            // Make sure that the width and height are valid.
            if (cropWidthPixels < 10.0f)
            {
                return false;
            }

            if (cropHeightPixels < 10.0f)
            {
                return false;
            }
            
            float angleOfUpDirectionRadians = Mathf.Atan2(textureBotLeftToTopLeft.y, textureBotLeftToTopLeft.x);
            float angleOfRightDirectionRadians = Mathf.Atan2(textureTopLeftToTopRight.y, textureTopLeftToTopRight.x);

            float angleDifferenceRadians = angleOfUpDirectionRadians - angleOfRightDirectionRadians;
            const float tau = Mathf.PI * 2.0f;
            angleDifferenceRadians = ((angleDifferenceRadians % tau) + tau) % tau; // Keep in range [0, tau)

            if (angleDifferenceRadians > Mathf.PI)
            {
                // Debug.Log("Image is flipped, which is not supported.");
                return false;
            }

            return true;
        }

        private Vector2 ToInputTexturePosition(Vector2 positionInInputContainer)
        {
            float rotationRadians = _inputRotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rotationRadians);
            float sin = Mathf.Sin(rotationRadians);
            
            Vector2 screenDisplayDimensions;
            if (WidthAndHeightSwapped)
            {
                screenDisplayDimensions = new(
                    _finalDisplaySize.y,
                    _finalDisplaySize.x
                );
            }
            else
            {
                screenDisplayDimensions = new(
                    _finalDisplaySize.x,
                    _finalDisplaySize.y
                );
            }
            
            Vector2 rotatedPositionRelativeToCenter = RotatePointAroundZero(positionInInputContainer, rotationRadians);
            
            // A value in the range [-1.0, 1.0]
            float offsetXRatio = (rotatedPositionRelativeToCenter.x * 2.0f) / _finalDisplaySize.x;
            float offsetYRatio = (rotatedPositionRelativeToCenter.y * 2.0f) / _finalDisplaySize.y;
            
            // Get the texture position. [0.0, 1.0]
            float textureXRatio = (offsetXRatio * 0.5f) + 0.5f;
            float textureYRatio = (offsetYRatio * 0.5f) + 0.5f;
            
            float textureX = textureXRatio * _tex.width;
            float textureY = textureYRatio * _tex.height;
            
            return new Vector2(textureX, textureY);
        }

        private static Vector2 RotatePointAroundZero(Vector2 point, float angleRadians)
        {
            float cos = Mathf.Cos(angleRadians);
            float sin = Mathf.Sin(angleRadians);
            return new Vector2(
                point.x * cos - point.y * sin,
                point.x * sin + point.y * cos
            );
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
                SetTopRightPositionBasedOnMouse(GetRelativeMousePositionInContainer());
            }
        }

        private void SetTopRightPositionBasedOnMouse(Vector2 mousePosition)
        {
            Vector2 topLeftToDesiredTopRight = mousePosition - _selectedPositionTopLeft;
                
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
        
        private bool WidthAndHeightSwapped => _inputRotationDegrees == 90 || _inputRotationDegrees == 270;

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
            
            Rect availableSpace = inputImageContainer.rect;
            float inputTextureAspect = (float)_tex.width / (float)_tex.height;
            if (WidthAndHeightSwapped)
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

            if (WidthAndHeightSwapped)
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

        private void TryDestroyOutputRenderTexture()
        {
            if (outputImageCamera != null)
            {
                outputImageCamera.targetTexture = null;
            }
            
            TryDestroyObject(ref _outputTexture);
        }
        
        private void TryDestroyGeneratedObjects()
        {
            TryDestroyObject(ref _tex);
            TryDestroyObject(ref _sprite);
            TryDestroyObject(ref _outputSprite);
            TryDestroyObject(ref _outputRenderTextureSprite);
            TryDestroyOutputRenderTexture();
        }

        private static void TryDestroyObject<T>(ref T obj) where T : Object
        {
            if (obj != null)
            {
                Object.Destroy(obj);
                obj = null;
            }
        }
    }
}