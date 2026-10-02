using UnityEngine;

namespace MegabonkSteve
{
    // Unity unloads unreferenced runtime assets on scene changes; this flags ours as permanent.
    internal static class Keep
    {
        public static T It<T>(T o) where T : UnityEngine.Object
        {
            if (o != null) o.hideFlags = HideFlags.HideAndDontSave;
            return o;
        }
    }
}
