using UnityEngine;

/// <summary>
/// Applies a Perlin-noise idle 3-D tilt to any UI RectTransform.
/// Attach to meld tile cards to match the hand-card idle animation.
/// </summary>
public class IdleTilt : MonoBehaviour
{
    const float MaxX = 10f, MaxY = 16f, Speed = 10f;

    RectTransform _rt;
    float _phase;

    void Awake()
    {
        _rt   = GetComponent<RectTransform>();
        _phase = Random.value * 100f;
    }

    void Update()
    {
        if (_rt == null) return;
        float tx = (Mathf.PerlinNoise(Time.time * 0.42f + _phase, 0.5f) - 0.5f) * 2f * MaxX;
        float ty = (Mathf.PerlinNoise(0.5f, Time.time * 0.32f + _phase) - 0.5f) * 2f * MaxY;
        _rt.localRotation = Quaternion.Slerp(
            _rt.localRotation,
            Quaternion.Euler(tx, ty, 0f),
            Time.deltaTime * Speed);
    }
}
