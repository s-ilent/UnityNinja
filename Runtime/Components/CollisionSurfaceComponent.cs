using System;
using UnityEngine;

namespace UnityNinja
{
    [Flags]
    public enum SA1SurfaceFlags : int
    {
        Solid                 = 0x1,
        Water                 = 0x2,
        NoFriction            = 0x4,
        NoAcceleration        = 0x8,
        LowAcceleration       = 0x10,
        UseSkyDrawDistance    = 0x20,
        CannotLand            = 0x40,
        IncreasedAcceleration = 0x80,
        Diggable              = 0x100,
        NoCamCollision        = 0x200,
        Waterfall             = 0x400,
        Slide                 = 0x800,
        Unclimbable           = 0x1000,
        Chaos0Land            = 0x2000,
        Stairs                = 0x4000,
        Lava                  = 0x8000,
        Hurt                  = 0x10000,
        Tube                  = 0x20000,
        LowDepth              = 0x40000,
        Simple                = 0x80000,
        Footprints            = 0x100000,
        Accelerate            = 0x200000,
        WaterCollision        = 0x400000,
        RotateByGravity       = 0x800000,
        NoZWrite              = 0x1000000,
        DrawByMesh            = 0x2000000,
        EnableManipulation    = 0x4000000,
        DynamicCollision      = 0x8000000,
        UseRotation           = 0x10000000,
        LargeCollision        = 0x20000000,
        MediumCollision       = 0x40000000,
        Visible               = unchecked((int)0x80000000)
    }

    [Flags]
    public enum SA2SurfaceFlags : int
    {
        Solid             = 0x1,
        Water             = 0x2,
        LowFriction       = 0x4,
        HighFriction      = 0x8,
        MediumFriction    = 0x10,
        Diggable          = 0x20,
        Unclimbable       = 0x80,
        Stairs            = 0x100,
        Hurt              = 0x400,
        Footsteps         = 0x800,
        CannotLand        = 0x1000,
        WaterSlowMove     = 0x2000,
        NoShadows         = 0x8000,
        IncreaseSpeed     = 0x100000,
        IncreaseAccel     = 0x200000,
        NoFogHighGravity  = 0x400000,
        MaxClip           = 0x800000,
        SimpleDraw        = 0x1000000,
        DirectDraw        = 0x2000000,
        NoCompile         = 0x4000000,
        DynamicCollision  = 0x8000000,
        NoRotateCollision = 0x10000000,
        SmallCollisionRad = 0x20000000,
        TinyCollisionRad  = 0x40000000,
        Visible           = unchecked((int)0x80000000)
    }

    [DisallowMultipleComponent]
    public class CollisionSurfaceComponent : MonoBehaviour
    {
        public int rawFlags;
        public int vertexCount;
        public int triangleCount;

        public SA1SurfaceFlags SA1Flags => (SA1SurfaceFlags)rawFlags;
        public SA2SurfaceFlags SA2Flags => (SA2SurfaceFlags)rawFlags;

        public bool HasSA1Flag(SA1SurfaceFlags flag) => (rawFlags & (int)flag) != 0;
        public bool HasSA2Flag(SA2SurfaceFlags flag) => (rawFlags & (int)flag) != 0;
    }
}