using UnityEngine;

/// <summary>Keeps a big tiled ground sprite under the camera, snapping by whole tiles so it looks endless.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GroundTiler : MonoBehaviour
{
    [SerializeField] float tileSize = 6f;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        transform.position = new Vector3(Mathf.Round(c.x / tileSize) * tileSize, Mathf.Round(c.y / tileSize) * tileSize, 0f);
    }
}
