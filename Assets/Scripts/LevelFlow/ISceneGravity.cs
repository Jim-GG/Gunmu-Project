using UnityEngine;

/// <summary>场景物理配置；未提供时使用全局关卡重力。</summary>
public interface ISceneGravity
{
    Vector3 Gravity { get; }
}
