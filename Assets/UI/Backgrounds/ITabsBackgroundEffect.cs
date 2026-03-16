using UnityEngine;

public interface ITabsBackgroundEffect
{
    void Initialize(Transform parent, GuitarBridgeServer owner);
    void Tick(float deltaTime);
    void Dispose();
}
