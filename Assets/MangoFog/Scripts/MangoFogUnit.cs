using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MangoFog
{
    /// <summary>
    /// The base MangoFogUnit class, attach this component to any object that should reveal in the fog
    /// </summary>
    public class MangoFogUnit : MonoBehaviour
    {
        /// <summary>
        /// The revealer type
        /// </summary>
        public RevealerType revealerType;

        /// <summary>
        /// The view radius of the unit
        /// </summary>
        public float viewRadius;

        /// <summary>
        /// The inner radius of an LOS type revealer
        /// </summary>
        public float LOSInnerRadius = 45f;

        /// <summary>
        /// the outer radius of an LOS type revealer
        /// </summary>
        public float LOSOuterRadius = 45f;

        /// <summary>
        /// Change this if your LOS view direction is the wrong way.
        /// </summary>
        public bool reverseLOSDirection = false;

        /// <summary>
        /// The bounds size multiplier. 
        /// Increase this by fractions of 0.1 if you see a brief tiny seam between fog chunks when walking through them.
        /// </summary>
        public float boundsSizeMultiplier;

        /// <summary>
        /// The center offset from the position of the unit
        /// </summary>
        public Vector3 centerOffset;

        /// <summary>
        /// Should the unit reveal on start?
        /// </summary>
        public bool autoRevealOnStart = true;

        /// <summary>
        /// The FOV relative to the rotation of an LOS type FogUnit.
        /// </summary>
        public float fovDegrees = 45f;

        /// <summary>
        /// Returns the fog unit position with the offset.
        /// </summary>
        /// <returns></returns>
        public Vector3 GetPosition() { return transform.position + centerOffset; }

        public Quaternion GetRotation() { return transform.rotation; }

        /// <summary>
        /// The revealer interface 
        /// </summary>
        protected MangoFogRevealer revealer;

        /// <summary>
        /// Returns true if the unit is active in the fog instance
        /// </summary>
        protected bool isActive = false;

        /// <summary>
        /// Inits the fog unit if it is not active
        /// </summary>
        public void StartRevealing()
		{
            if(!isActive)
                Init();
		}

        /// <summary>
        /// Removes the revealer from the instance list
        /// </summary>
        public void StopRevealing()
		{
            MangoFogInstance.Instance.RemoveRevealer(revealer);
            isActive = false;
        }

        /// <summary>
        /// Reveals on start if autoRevealOnStart is true
        /// </summary>
        protected virtual void Start()
		{
            if(autoRevealOnStart)
                StartRevealing();
		}

        /// <summary>
        /// Creates a new fog revealer and adds it to the instance list
        /// </summary>
        protected virtual void Init()
		{
            revealer = new MangoFogRevealer();
            revealer.SetUnit(this);
            revealer.SetRevealerType(revealerType);
            revealer.SetPosition(transform.position);
            revealer.SetRadius(viewRadius);
            revealer.SetUniqueID(gameObject.GetInstanceID());
            revealer.SetFOVDegrees(fovDegrees);
            revealer.SetLOSInnerRadius(LOSInnerRadius);
            revealer.SetLOSOuterRadius(LOSOuterRadius);
            revealer.SetReverseLOSDirection(reverseLOSDirection);
            revealer.SetBounds(new Bounds(transform.position, new Vector3((viewRadius * 2) * boundsSizeMultiplier,
                (viewRadius * 2) * boundsSizeMultiplier,
                (viewRadius * 2) * boundsSizeMultiplier)));
            MangoFogInstance.Instance.AddRevealer(revealer);
            isActive = true;
        }
    }
}

