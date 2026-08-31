using System;
using UnityEngine;
using UnityEditor;

namespace UnityNinja.Editor
{
    [Flags]
    public enum NJSMaterialFlags : uint
    {
        ClampV         = 0x8000,
        ClampU         = 0x10000,
        FlipV          = 0x20000,
        FlipU          = 0x40000,
        UseAlpha       = 0x100000,
        UseTexture     = 0x200000,
        EnvironmentMap = 0x400000,
        DoubleSided    = 0x800000,
        IgnoreLighting = 0x2000000
    }

    public class NinjaShaderGUI : ShaderGUI
    {
        public enum RenderMode
        {
            Opaque,
            Cutout,
            Transparent,
            Fade,
            Additive,
            Multiply,
            ReverseSubtract,
            Custom
        }

        private MaterialProperty modeProp;
        private MaterialProperty srcBlendProp;
        private MaterialProperty dstBlendProp;
        private MaterialProperty blendOpProp;
        private MaterialProperty zWriteProp;
        private MaterialProperty zTestProp;
        private MaterialProperty cullProp;
        private MaterialProperty customRenderQueueProp;

        private MaterialProperty colorProp;
        private MaterialProperty ambientColorProp;
        private MaterialProperty mainTexProp;
        private MaterialProperty alphaTestProp;
        private MaterialProperty cutoffProp;
        private MaterialProperty alphaToMaskProp;

        private MaterialProperty specColorProp;
        private MaterialProperty shininessProp;
        private MaterialProperty unlitProp;

        private MaterialProperty useEnvMapProp;
        private MaterialProperty addEnvMapProp;
        private MaterialProperty envMapProp;
        private MaterialProperty envColorProp;
        private MaterialProperty envPowerProp;

        private MaterialProperty clampUProp;
        private MaterialProperty clampVProp;
        private MaterialProperty flipUProp;
        private MaterialProperty flipVProp;

        private MaterialProperty materialFlagsProp;

        public void FindProperties(MaterialProperty[] props)
        {
            modeProp = FindProperty("_Mode", props, false);
            srcBlendProp = FindProperty("_SrcBlend", props, false);
            dstBlendProp = FindProperty("_DstBlend", props, false);
            blendOpProp = FindProperty("_BlendOp", props, false);
            zWriteProp = FindProperty("_ZWrite", props, false);
            zTestProp = FindProperty("_ZTest", props, false);
            cullProp = FindProperty("_Cull", props, false);
            customRenderQueueProp = FindProperty("_CustomRenderQueue", props, false);

            colorProp = FindProperty("_Color", props, false);
            ambientColorProp = FindProperty("_AmbientColor", props, false);
            mainTexProp = FindProperty("_MainTex", props, false);
            alphaTestProp = FindProperty("_AlphaTest", props, false);
            cutoffProp = FindProperty("_Cutoff", props, false);
            alphaToMaskProp = FindProperty("_AlphaToMask", props, false);

            specColorProp = FindProperty("_SpecColor", props, false);
            shininessProp = FindProperty("_Shininess", props, false);
            unlitProp = FindProperty("_Unlit", props, false);

            useEnvMapProp = FindProperty("_UseEnvMap", props, false);
            addEnvMapProp = FindProperty("_AddEnvMap", props, false);
            envMapProp = FindProperty("_EnvMap", props, false);
            envColorProp = FindProperty("_EnvColor", props, false);
            envPowerProp = FindProperty("_EnvPower", props, false);

            clampUProp = FindProperty("_ClampU", props, false);
            clampVProp = FindProperty("_ClampV", props, false);
            flipUProp = FindProperty("_FlipU", props, false);
            flipVProp = FindProperty("_FlipV", props, false);

            materialFlagsProp = FindProperty("_MaterialFlags", props, false);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            FindProperties(properties);
            Material targetMat = materialEditor.target as Material;
            if (targetMat == null) return;

            EditorGUI.BeginChangeCheck();

            // 1. Primary Surface
            EditorGUILayout.LabelField("Ninja Surface Parameters", EditorStyles.boldLabel);
            if (mainTexProp != null && colorProp != null)
                materialEditor.TexturePropertySingleLine(new GUIContent("Main Texture"), mainTexProp, colorProp);

            if (ambientColorProp != null) materialEditor.ShaderProperty(ambientColorProp, "Ambient Color");

            if (alphaTestProp != null)
            {
                // materialEditor.ShaderProperty(alphaTestProp, "Enable Alpha Cutout");
                if (alphaTestProp.floatValue > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    if (cutoffProp != null) materialEditor.ShaderProperty(cutoffProp, "Cutoff Threshold");
                    if (alphaToMaskProp != null) materialEditor.ShaderProperty(alphaToMaskProp, "Alpha to Coverage (MSAA)");
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space();

            // 2. Environment Reflection
            EditorGUILayout.LabelField("Environment Reflection", EditorStyles.boldLabel);
            if (useEnvMapProp != null)
            {
                materialEditor.ShaderProperty(useEnvMapProp, "Base Texture as EnvMap (Replace UVs)");
            }
            if (addEnvMapProp != null)
            {
                materialEditor.ShaderProperty(addEnvMapProp, "Add Second Layer EnvMap Reflection");
                if (addEnvMapProp.floatValue > 0.5f && envMapProp != null)
                {
                    EditorGUI.indentLevel++;
                    if (envColorProp != null)
                        materialEditor.TexturePropertySingleLine(new GUIContent("Reflection Map"), envMapProp, envColorProp);
                    else
                        materialEditor.TexturePropertySingleLine(new GUIContent("Reflection Map"), envMapProp);

                    if (envPowerProp != null) materialEditor.ShaderProperty(envPowerProp, "Reflection Multiplier");
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space();

            // 3. UV Wrap & Clamping (TileMode)
            if (clampUProp != null || clampVProp != null || flipUProp != null || flipVProp != null)
            {
                EditorGUILayout.LabelField("UV Wrap & Clamping (TileMode)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                if (clampUProp != null) materialEditor.ShaderProperty(clampUProp, "Clamp U");
                if (clampVProp != null) materialEditor.ShaderProperty(clampVProp, "Clamp V");
                if (flipUProp != null) materialEditor.ShaderProperty(flipUProp, "Flip / Mirror U");
                if (flipVProp != null) materialEditor.ShaderProperty(flipVProp, "Flip / Mirror V");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            // 4. Lighting & Specular
            EditorGUILayout.LabelField("Lighting & Specular", EditorStyles.boldLabel);
            if (specColorProp != null) materialEditor.ShaderProperty(specColorProp, "Specular Color");
            if (shininessProp != null) materialEditor.ShaderProperty(shininessProp, "Shininess / Exponent");
            if (unlitProp != null) materialEditor.ShaderProperty(unlitProp, "Unlit / Bypass Lighting");

            EditorGUILayout.Space();

            // 5. Render & Blend Settings
            if (modeProp != null)
            {
                EditorGUILayout.LabelField("Render & Blend Settings", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                RenderMode mode = (RenderMode)modeProp.floatValue;
                mode = (RenderMode)EditorGUILayout.EnumPopup("Rendering Mode", mode);
                if (EditorGUI.EndChangeCheck())
                {
                    materialEditor.RegisterPropertyChangeUndo("Rendering Mode");
                    modeProp.floatValue = (float)mode;
                    SetupMaterialWithRenderMode(targetMat, mode);
                }

                EditorGUI.indentLevel++;
                if (srcBlendProp != null) materialEditor.ShaderProperty(srcBlendProp, "Source Blend");
                if (dstBlendProp != null) materialEditor.ShaderProperty(dstBlendProp, "Destination Blend");
                if (blendOpProp != null) materialEditor.ShaderProperty(blendOpProp, "Blend Operation");
                if (zWriteProp != null) materialEditor.ShaderProperty(zWriteProp, "Depth Write");
                if (zTestProp != null) materialEditor.ShaderProperty(zTestProp, "Depth Test");
                if (cullProp != null) materialEditor.ShaderProperty(cullProp, "Cull Mode");

                if (customRenderQueueProp != null)
                {
                    materialEditor.ShaderProperty(customRenderQueueProp, "Custom Render Queue");
                    if (customRenderQueueProp.floatValue >= 0)
                    {
                        targetMat.renderQueue = (int)customRenderQueueProp.floatValue;
                    }
                }
                EditorGUI.indentLevel--;
            }

            // 6. Raw Flags Bitmask Display
            if (materialFlagsProp != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Ninja Material Flags", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                uint rawFlags = (uint)materialFlagsProp.floatValue;
                EditorGUILayout.LabelField("Raw Hex Value:", $"0x{rawFlags:X8}");

                EditorGUI.BeginChangeCheck();
                NJSMaterialFlags flagsEnum = (NJSMaterialFlags)(rawFlags & 0x03F78000u);
                flagsEnum = (NJSMaterialFlags)EditorGUILayout.EnumFlagsField("Active Bitmask", flagsEnum);
                if (EditorGUI.EndChangeCheck())
                {
                    uint newFlags = (rawFlags & ~0x03F78000u) | (uint)flagsEnum;
                    materialFlagsProp.floatValue = (float)newFlags;

                    if (clampUProp != null) clampUProp.floatValue = ((newFlags & (uint)NJSMaterialFlags.ClampU) != 0) ? 1.0f : 0.0f;
                    if (clampVProp != null) clampVProp.floatValue = ((newFlags & (uint)NJSMaterialFlags.ClampV) != 0) ? 1.0f : 0.0f;
                    if (flipUProp != null) flipUProp.floatValue = ((newFlags & (uint)NJSMaterialFlags.FlipU) != 0) ? 1.0f : 0.0f;
                    if (flipVProp != null) flipVProp.floatValue = ((newFlags & (uint)NJSMaterialFlags.FlipV) != 0) ? 1.0f : 0.0f;
                    if (useEnvMapProp != null) useEnvMapProp.floatValue = ((newFlags & (uint)NJSMaterialFlags.EnvironmentMap) != 0) ? 1.0f : 0.0f;
                    if (unlitProp != null) unlitProp.floatValue = ((newFlags & (uint)NJSMaterialFlags.IgnoreLighting) != 0) ? 1.0f : 0.0f;
                    if (cullProp != null) cullProp.floatValue = ((newFlags & (uint)NJSMaterialFlags.DoubleSided) != 0) ? (float)UnityEngine.Rendering.CullMode.Off : (float)UnityEngine.Rendering.CullMode.Back;
                }

                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
            {
                foreach (var obj in materialEditor.targets)
                {
                    MaterialChanged((Material)obj);
                }
            }
        }

        private static void MaterialChanged(Material material)
        {
            if (material != null && material.HasProperty("_Mode"))
            {
                SetupMaterialWithRenderMode(material, (RenderMode)material.GetFloat("_Mode"));
            }
        }

        public static void SetupMaterialWithRenderMode(Material material, RenderMode mode)
        {
            switch (mode)
            {
                case RenderMode.Opaque:
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.SetOverrideTag("Queue", "Geometry");
                    material.SetOverrideTag("IgnoreProjector", "False");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 1);
                    material.SetFloat("_AlphaTest", 0.0f);
                    material.SetFloat("_AlphaToMask", 0.0f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    material.SetShaderPassEnabled("ShadowCaster", true);
                    material.SetShaderPassEnabled("DepthOnly", true);
                    break;

                case RenderMode.Cutout:
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.SetOverrideTag("Queue", "AlphaTest");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 1);
                    material.SetFloat("_AlphaTest", 1.0f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    material.SetShaderPassEnabled("ShadowCaster", true);
                    material.SetShaderPassEnabled("DepthOnly", true);
                    break;

                case RenderMode.Transparent:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    material.SetFloat("_AlphaToMask", 0.0f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.Fade:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    material.SetFloat("_AlphaToMask", 0.0f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.Additive:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    material.SetFloat("_AlphaToMask", 0.0f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.Multiply:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    material.SetFloat("_AlphaToMask", 0.0f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.ReverseSubtract:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("Queue", "Transparent");
                    material.SetOverrideTag("IgnoreProjector", "True");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.ReverseSubtract);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaTest", 0.0f);
                    material.SetFloat("_AlphaToMask", 0.0f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    break;

                case RenderMode.Custom:
                    break;
            }
        }
    }
}