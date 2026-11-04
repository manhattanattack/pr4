using UnityEngine;

public interface INoiseSource
{
    Vector3 Position { get; }
    float NoiseRadius { get; }
}