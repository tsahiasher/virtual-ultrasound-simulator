using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Volume
{
    /// <summary>
    /// Implements IVolumeSampler by querying a SyntheticAnatomyVolume.
    /// Acts as the modular bridge between volume data and the slice generation pipeline.
    /// </summary>
    public class ProceduralVolumeSampler : MonoBehaviour, IVolumeSampler
    {
        [SerializeField] private SyntheticAnatomyVolume anatomyVolume;

        public SyntheticAnatomyVolume AnatomyVolume
        {
            get => anatomyVolume;
            set => anatomyVolume = value;
        }

        private void Awake()
        {
            if (anatomyVolume == null)
            {
                anatomyVolume = GetComponent<SyntheticAnatomyVolume>();
                if (anatomyVolume == null)
                {
                    anatomyVolume = FindObjectOfType<SyntheticAnatomyVolume>();
                }
            }
        }

        public SampleResult SampleWorld(Vector3 worldPosition)
        {
            if (anatomyVolume == null)
            {
                return SampleResult.Empty;
            }

            return anatomyVolume.EvaluateSample(worldPosition);
        }
    }
}
