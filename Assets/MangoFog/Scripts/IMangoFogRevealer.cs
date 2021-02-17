using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MangoFog
{
    public interface IMangoFogRevealer
    {
        bool IsValid();
        RevealerType GetRevealerType();
        Vector3 GetPosition();
        Bounds GetBounds();
        float GetRadius();
        Quaternion GetRot();
        bool DoReverseLOSDirection();
        float GetLOSInnerRadius();
        float GetLOSOuterRadius();
        float GetFOVDegrees();
        float GetFOVCosine();
        int GetUniqueID();
        void Update(int deltaMS);
        void Release();
        void Invalidate();
    }
}

