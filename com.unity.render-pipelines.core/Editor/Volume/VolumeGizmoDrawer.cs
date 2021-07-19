using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering
{
    /// <summary>
    /// Draws Volumes Gizmos
    /// </summary>
    public class VolumeGizmoDrawer
    {
        private static List<Collider> s_TempColliders = new List<Collider>();

        [DrawGizmo(GizmoType.Active | GizmoType.Selected | GizmoType.NonSelected)]
        static void OnDrawGizmos(Volume scr, GizmoType gizmoType)
        {
            if (!scr.enabled)
                return;

            s_TempColliders.Clear();

            var colliders = s_TempColliders;
            scr.GetComponents(colliders);

            if (scr.isGlobal || colliders == null)
                return;

            // Store the computation of the lossyScale
            var lossyScale = scr.transform.lossyScale;
            Gizmos.matrix = Matrix4x4.TRS(scr.transform.position, scr.transform.rotation, lossyScale);
            Gizmos.color = VolumesPreferences.volumeGizmoColor;

            // Draw a separate gizmo for each collider
            foreach (var collider in colliders)
            {
                if (!collider.enabled)
                    continue;

                // We'll just use scaling as an approximation for volume skin. It's far from being
                // correct (and is completely wrong in some cases). Ultimately we'd use a distance
                // field or at least a tesselate + push modifier on the collider's mesh to get a
                // better approximation, but the current Gizmo system is a bit limited and because
                // everything is dynamic in Unity and can be changed at anytime, it's hard to keep
                // track of changes in an elegant way (which we'd need to implement a nice cache
                // system for generated volume meshes).
                switch (collider)
                {
                    case BoxCollider c:
                        if (VolumesPreferences.drawWireFrame)
                            Gizmos.DrawWireCube(c.center, c.size);
                        if (VolumesPreferences.drawSolid)
                            Gizmos.DrawCube(c.center, c.size);
                        break;
                    case SphereCollider c:
                        // For sphere the only scale that is used is the transform.scale.x
                        Gizmos.matrix = Matrix4x4.TRS(scr.transform.position, scr.transform.rotation, Vector3.one * lossyScale.x);
                        if (VolumesPreferences.drawWireFrame)
                            Gizmos.DrawWireSphere(c.center, c.radius);
                        if (VolumesPreferences.drawSolid)
                            Gizmos.DrawSphere(c.center, c.radius);
                        break;
                    case MeshCollider c:
                        // Only convex mesh m_Colliders are allowed
                        if (!c.convex)
                            c.convex = true;

                        if (VolumesPreferences.drawWireFrame)
                            Gizmos.DrawWireMesh(c.sharedMesh);
                        if (VolumesPreferences.drawSolid)
                            // Mesh pivot should be centered or this won't work
                            Gizmos.DrawMesh(c.sharedMesh);
                        break;
                    default:
                        // Nothing for capsule (DrawCapsule isn't exposed in Gizmo), terrain, wheel and
                        // other m_Colliders...
                        break;
                }
            }

            colliders.Clear();
        }
    }
}
