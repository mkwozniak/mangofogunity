using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MangoFog
{
	public class MangoFogRevealer : IMangoFogRevealer
	{
		//the fog revealer members
		protected RevealerType revealerType;
		protected MangoFogUnit unit;

		protected bool valid = true;
		protected bool reverseLOSDir = false;
		protected int uniqueID = 0;

		protected float radius;
		protected float losInnerRadius;
		protected float losOuterRadius;

		protected Vector3 position;
		protected Bounds bounds;

		protected Quaternion rot = Quaternion.identity;
		protected float fovCosine = Mathf.Cos(Mathf.Deg2Rad * 45f);

		public float fovDegrees = 45f;

		//get set
		public RevealerType GetRevealerType() { return revealerType; }
		public Vector3 GetPosition() { return position; }
		public float GetRadius() { return radius; }
		public Bounds GetBounds() { return bounds; }
		public int GetUniqueID() { return this.uniqueID; }
		public Quaternion GetRot() { return rot; }
		public float GetLOSInnerRadius() { return losInnerRadius; }
		public float GetLOSOuterRadius() { return losOuterRadius; }
		public float GetFOVDegrees() { return fovDegrees; }
		public float GetFOVCosine() { return Mathf.Cos(Mathf.Deg2Rad * fovDegrees); }
		public void SetReverseLOSDirection(bool val) { reverseLOSDir = val; }
		public void SetRevealerType(RevealerType type) { revealerType = type; }
		public void SetUniqueID(int id) { this.uniqueID = id; }
		public void SetUnit(MangoFogUnit unit) { this.unit = unit; }
		public void SetPosition(Vector3 position) { this.position = position; }
		public void SetRadius(float radius) { this.radius = radius; }
		public void SetBounds(Bounds bounds) { this.bounds = bounds; }
		public void SetFOVDegrees(float fovDegrees) { this.fovDegrees = fovDegrees; }
		public void SetLOSInnerRadius(float rad) { losInnerRadius = rad; }
		public void SetLOSOuterRadius(float rad) { losOuterRadius = rad; }
		public bool DoReverseLOSDirection() { return reverseLOSDir; }
		public void Invalidate() { this.valid = false;}
		public bool IsValid() { return this.valid; }
		public void Release() { unit.StopRevealing(); }

		//updates the revealer values from its unit owner
		public void Update(int deltaMS)
		{
			position = unit.GetPosition();
			rot = unit.GetRotation();
			bounds.center = position;
		}
	}
}

