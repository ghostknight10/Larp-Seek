using UnityEngine;

/// <summary>
/// Attached to stationary decoy clones placed by Hiders (Right Click).
/// Allows Hunter's shots to hit and destroy decoys.
/// </summary>
public class DecoyTarget : MonoBehaviour
{
    public ulong OwnerClientId { get; set; }
    public NetworkPlayerController OwnerPlayer { get; set; }

    void Awake()
    {
        // Ensure a collider exists so raycasts from Hunter can hit it
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                    b.Encapsulate(rends[i].bounds);
                box.center = transform.InverseTransformPoint(b.center);
                Vector3 lossy = transform.lossyScale;
                box.size = new Vector3(
                    lossy.x != 0f ? Mathf.Abs(b.size.x / lossy.x) : b.size.x,
                    lossy.y != 0f ? Mathf.Abs(b.size.y / lossy.y) : b.size.y,
                    lossy.z != 0f ? Mathf.Abs(b.size.z / lossy.z) : b.size.z
                );
            }
            else
            {
                box.size = new Vector3(1f, 1.5f, 1f);
                box.center = new Vector3(0f, 0.75f, 0f);
            }
        }
    }

    public void OnShotByHunter()
    {
        ProceduralAudio.PlayDecoyPuff(transform.position);

        if (OwnerPlayer != null)
            OwnerPlayer.OnDecoyDestroyed(gameObject);

        Destroy(gameObject);
    }
}
