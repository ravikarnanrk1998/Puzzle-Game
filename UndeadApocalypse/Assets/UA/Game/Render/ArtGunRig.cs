using UnityEngine;

namespace UA.Game
{
    /// <summary>
    /// Anchor points of an imported weapon prefab (gun space: +Z muzzle, +Y up), matching the slots the
    /// procedural <see cref="GunModel"/> provides. Moving parts sit under pivots with identity rotation
    /// so the existing slide / magazine animation code moves them along the gun's own axes.
    /// (Own file: Unity can only serialize a MonoBehaviour into a prefab when the file name matches.)
    /// </summary>
    public sealed class ArtGunRig : MonoBehaviour
    {
        public Transform Muzzle, Sight, GripR, GripL, MagWell, Eject;
        public Transform Slide, Mag, Bolt;
        public float Length = 0.2f;
        public bool HasScope;
    }
}
