using UnityEngine;
using UnityEngine.UI;

namespace VirtualUltrasound.Rendering
{
    /// <summary>
    /// UI component that presents the 2D ultrasound slice image with medical ultrasound overlays
    /// (orientation marker dot, depth scale tick marks, and aspect ratio preservation).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class UltrasoundDisplay : MonoBehaviour
    {
        [SerializeField] private SliceRenderer sliceRenderer;
        [SerializeField] private RawImage targetImage;
        [SerializeField] private AspectRatioFitter aspectFitter;

        private void Awake()
        {
            if (targetImage == null) targetImage = GetComponent<RawImage>();
            if (aspectFitter == null) aspectFitter = GetComponent<AspectRatioFitter>();
            if (sliceRenderer == null) sliceRenderer = FindObjectOfType<SliceRenderer>();
        }

        private void OnEnable()
        {
            if (sliceRenderer != null)
            {
                sliceRenderer.OnTextureUpdated += HandleTextureUpdated;
                if (sliceRenderer.SliceTexture != null)
                {
                    HandleTextureUpdated(sliceRenderer.SliceTexture);
                }
            }
        }

        private void OnDisable()
        {
            if (sliceRenderer != null)
            {
                sliceRenderer.OnTextureUpdated -= HandleTextureUpdated;
            }
        }

        public void BindSliceRenderer(SliceRenderer renderer)
        {
            if (sliceRenderer != null)
            {
                sliceRenderer.OnTextureUpdated -= HandleTextureUpdated;
            }

            sliceRenderer = renderer;
            if (sliceRenderer != null)
            {
                sliceRenderer.OnTextureUpdated += HandleTextureUpdated;
                if (sliceRenderer.SliceTexture != null)
                {
                    HandleTextureUpdated(sliceRenderer.SliceTexture);
                }
            }
        }

        private void HandleTextureUpdated(Texture2D texture)
        {
            if (targetImage != null && texture != null)
            {
                targetImage.texture = texture;
                if (aspectFitter != null)
                {
                    aspectFitter.aspectRatio = (float)texture.width / texture.height;
                }
            }
        }
    }
}
