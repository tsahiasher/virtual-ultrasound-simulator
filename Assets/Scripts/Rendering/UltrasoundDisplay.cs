using UnityEngine;
using UnityEngine.UI;

namespace VirtualUltrasound.Rendering
{
    /// <summary>
    /// UI component that presents the 2D ultrasound slice image with medical ultrasound overlays.
    /// Supports both Texture2D (CPU reference) and RenderTexture (GPU acceleration) seamlessly.
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
                if (sliceRenderer.ActiveTexture != null)
                {
                    HandleTextureUpdated(sliceRenderer.ActiveTexture);
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
                if (sliceRenderer.ActiveTexture != null)
                {
                    HandleTextureUpdated(sliceRenderer.ActiveTexture);
                }
            }
        }

        private void Update()
        {
            if (sliceRenderer == null) return;

            if (targetImage != null && sliceRenderer != null && sliceRenderer.ActiveTexture != null && targetImage.texture != sliceRenderer.ActiveTexture)
            {
                HandleTextureUpdated(sliceRenderer.ActiveTexture);
            }
        }

        private void HandleTextureUpdated(Texture texture)
        {
            if (targetImage != null && texture != null)
            {
                targetImage.texture = texture;
                if (aspectFitter != null && texture.height > 0)
                {
                    aspectFitter.aspectRatio = (float)texture.width / texture.height;
                }
            }
        }
    }
}
