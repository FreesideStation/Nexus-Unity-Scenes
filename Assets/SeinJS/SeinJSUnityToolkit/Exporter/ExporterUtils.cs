﻿/**
 * @File   : ExportorUtils.cs
 * @Author : dtysky (dtysky@outlook.com)
 * @Link   : dtysky.moe
 * @Date   : 2019/09/09 0:00:00PM
 */
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using GLTF.Schema;
using System.IO;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using XREngine;
using XREngine.RealityPack;

namespace SeinJS
{
    public class ExporterUtils
    {
        private static GameObject _tempGO = null;

        public static Regex rgxPath = new Regex("[^a-zA-Z0-9-_./]");
        public static string CleanPath(string s)
        {
            return rgxPath.Replace(s, "").ToLower();
        }

        public static Vector4[] BoneWeightToWeightVec4(BoneWeight[] bw)
        {
            Vector4[] weights = new Vector4[bw.Length];
            for (int i = 0; i < bw.Length; ++i)
            {
                weights[i] = new Vector4(bw[i].weight0, bw[i].weight1, bw[i].weight2, bw[i].weight3);
            }

            return weights;
        }

        public static Accessor PackToBuffer<DataType>(
            byte[] buffer, DataType[] data,
            GLTFComponentType componentType,
            int offset, int stride,
            Func<DataType[], int, DataType> getValueByIndex = null
        )
        {
            var accessor = new Accessor();
            
            accessor.ByteOffset = offset;
            accessor.ComponentType = componentType;

            double[] max = null;
            double[] min = null;
            
            for (int i = 0; i < data.Length; i += 1)
            {
                var bytes = GetDataToBuffer(getValueByIndex == null ? data[i] : getValueByIndex(data, i), componentType, ref max, ref min, ref accessor.Type);
                int byteOffset = stride * i; // offset + stride * i;

                bytes.CopyTo(buffer, byteOffset); 
            }

            accessor.Count = data.Length;
            accessor.Max = max.ToList();
            accessor.Min = min.ToList();

            return accessor;
        }

        public static Accessor PackToBuffer<DataType>(
            MemoryStream stream, DataType[] data,
            GLTFComponentType componentType,
            Func<DataType[], int, DataType> getValueByIndex = null
        )
        {
            var accessor = new Accessor();
            
            accessor.ComponentType = componentType;

            double[] max = null;
            double[] min = null;

            for (int i = 0; i < data.Length; i += 1)
            {
                var bytes = GetDataToBuffer(getValueByIndex == null ? data[i] : getValueByIndex(data, i), componentType, ref max, ref min, ref accessor.Type);

                stream.Write(bytes, 0, bytes.Length);
            }
            accessor.ByteOffset = 0;
            accessor.Count = data.Length;
            accessor.Max = max.ToList();
            accessor.Min = min.ToList();
            

            return accessor;
        }
        
        private static byte[] GetDataToBuffer<DataType>(
            DataType value, GLTFComponentType componentType,
            ref double[] max, ref double[] min, ref GLTFAccessorAttributeType type
        )
        {
            float[] array = null;
            int[] intArray = null;
            int size = 0;
            /**
             @todo: support int uint short byte ushort...
             */
            if (typeof(DataType) == typeof(float))
            {
                size = 1;
                type = GLTFAccessorAttributeType.SCALAR;
                var v = (float)Convert.ChangeType(value, typeof(float));
                array = new float[] { v };
            }
            else if (typeof(DataType) == typeof(int))
            {
                size = 1;
                type = GLTFAccessorAttributeType.SCALAR;
                var v = (int)Convert.ChangeType(value, typeof(int));
                intArray = new int[] { v };
            }
            else if (typeof(DataType) == typeof(Vector2))
            {
                size = 2;
                type = GLTFAccessorAttributeType.VEC2;
                var v = (Vector2)Convert.ChangeType(value, typeof(Vector2));
                array = new float[] { v.x, v.y };
            }
            else if (typeof(DataType) == typeof(Vector3))
            {
                size = 3;
                type = GLTFAccessorAttributeType.VEC3;
                var v = (Vector3)Convert.ChangeType(value, typeof(Vector3));
                array = new float[] { v.x, v.y, v.z };
            }
            else if (typeof(DataType) == typeof(Vector4))
            {
                size = 4;
                type = GLTFAccessorAttributeType.VEC4;
                var v = (Vector4)Convert.ChangeType(value, typeof(Vector4));
                array = new float[] { v.x, v.y, v.z, v.w };
            }
            else if (typeof(DataType) == typeof(Color))
            {
                size = 4;
                type = GLTFAccessorAttributeType.VEC4;
                var v = (Color)Convert.ChangeType(value, typeof(Color));
                array = new float[] { v.r, v.g, v.b, v.a };
            }
            else if (typeof(DataType) == typeof(BoneWeight))
            {
                size = 4;
                type = GLTFAccessorAttributeType.VEC4;
                var v = (BoneWeight)Convert.ChangeType(value, typeof(BoneWeight));
                intArray = new int[] { v.boneIndex0, v.boneIndex1, v.boneIndex2, v.boneIndex3 };
            }
            else if (typeof(DataType) == typeof(Matrix4x4))
            {
                size = 16;
                type = GLTFAccessorAttributeType.MAT4;
                var v = (Matrix4x4)Convert.ChangeType(value, typeof(Matrix4x4));
                array = new float[] {
                    v.m00, v.m10, v.m20, v.m30,
                    v.m01, v.m11, v.m21, v.m31,
                    v.m02, v.m12, v.m22, v.m32,
                    v.m03, v.m13, v.m23, v.m33
                };
            }
            else
            {
                Utils.ThrowExcption("Only support packing float, int, Vector2, Vector3, Vector4, BoneWeight, Matrix4 and Color now !");
            }

            if (max == null)
            {
                max = new double[size];
                min = new double[size];

                for (int i = 0; i < size;  i += 1)
                {
                    var v = intArray == null ? array[i] : intArray[i];
                    max[i] = v;
                    min[i] = v;
                }
            }

            for (int i = 0; i < size; i += 1)
            {
                var v = intArray == null ? array[i] : intArray[i];
                max[i] = max[i] > v ? max[i] : v;
                min[i] = min[i] < v ? min[i] : v;
            }

            if (intArray != null)
            {
                return GetBytes(intArray, componentType);
            }

            return GetBytes(array, componentType);
        }

        public static Accessor PackToBufferFloatArray(
            MemoryStream stream, float[] data,
            GLTFAccessorAttributeType attributeType,
            GLTFComponentType componentType
        )
        {
            
            var accessor = new Accessor();
            
            accessor.ByteOffset = (int)stream.Length;
            accessor.ComponentType = componentType;
            accessor.Type = attributeType;

            int elementSize = attributeType == GLTFAccessorAttributeType.VEC2 ? 2 : attributeType == GLTFAccessorAttributeType.VEC3 ? 3 : attributeType == GLTFAccessorAttributeType.VEC4 ? 4 : 1;

            // no need to calc max and min for animation
            var bytes = GetBytes(data, componentType);
            stream.Write(bytes, 0, bytes.Length);
            accessor.Count = data.Length / elementSize;

            return accessor;
        }

        private static byte[] GetBytes(float[] array, GLTFComponentType componentType)
        {
            int size = 0;
            Array dArray = null;

            switch (componentType)
            {
                case GLTFComponentType.Byte:
                case GLTFComponentType.UnsignedByte:
                    size = 1;
                    dArray = Array.ConvertAll(array, item => (byte)item);
                    break;
                case GLTFComponentType.Short:
                    size = 2;
                    dArray = Array.ConvertAll(array, item => (short)item);
                    break;
                case GLTFComponentType.UnsignedShort:
                    size = 2;
                    dArray = Array.ConvertAll(array, item => (ushort)item);
                    break;
                case GLTFComponentType.Float:
                    size = 4;
                    dArray = array;
                    break;
                case GLTFComponentType.UnsignedInt:
                    size = 4;
                    dArray = Array.ConvertAll(array, item => (uint)item);
                    break;
            }

            var bytes = new byte[size * array.Length];
            System.Buffer.BlockCopy(dArray, 0, bytes, 0, bytes.Length);

            return bytes;
        }

        private static byte[] GetBytes(int[] array, GLTFComponentType componentType)
        {
            int size = 0;
            Array dArray = null;

            switch (componentType)
            {
                case GLTFComponentType.Byte:
                case GLTFComponentType.UnsignedByte:
                    size = 1;
                    dArray = Array.ConvertAll(array, item => (byte)item);
                    break;
                case GLTFComponentType.Short:
                    size = 2;
                    dArray = Array.ConvertAll(array, item => (short)item);
                    break;
                case GLTFComponentType.UnsignedShort:
                    size = 2;
                    dArray = Array.ConvertAll(array, item => (ushort)item);
                    break;
                case GLTFComponentType.Float:
                    size = 4;
                    dArray = array;
                    break;
                case GLTFComponentType.UnsignedInt:
                    size = 4;
                    dArray = Array.ConvertAll(array, item => (uint)item);
                    break;
            }

            var bytes = new byte[size * array.Length];
            System.Buffer.BlockCopy(dArray, 0, bytes, 0, bytes.Length);

            return bytes;
        }

        public static GLTF.Schema.Material ConvertMaterial(UnityEngine.Material material, ExporterEntry entry, Renderer renderer = null)
        {
            if (material.shader.name.Contains("Standard") || material.shader.name.Contains("Autodesk Interactive"))
            {
                Utils.ThrowExcption("Toolkit doesn't support Unity Standard Material anymore, please use converter to convert them to 'Sein/PBR', check here: http://seinjs.com/cn/guide/scene-editor/material-sein");
            }

            if (material.shader.name == "Sein/PBR")
            {
                return ConvertSeinPBRMaterial(material, entry, renderer);
            }

            if (material.shader.name.Contains("Sein/"))
            {
                return ConvertSeinCustomMaterial(material, entry);
            }

            //return ConvertKHRWebGLMaterial(material, entry);
            Utils.ThrowExcption("Only support Sein/PBR or Sein/XXX(CustomMaterial) now !");
            return null;
        }

        private static Texture2D GenerateBakedDiffuse(Texture2D tex, Texture2D lightmapTex, UnityEngine.Mesh mesh, Renderer renderer)
        {
            //AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(tex), "Assets/Debug/tex.png");
            //AssetDatabase.CreateAsset(tex, "Assets/Debug/tex.asset");
            //AssetDatabase.CreateAsset(lightmapTex, "Assets/Debug/lightmapTex.asset");
            ComputeShader combiner = AssetDatabase.LoadAssetAtPath<ComputeShader>(@"Assets/XREngine/Code/Shaders/LightmapCombineBaker.compute");

            combiner.SetVector("lightmapScaleOffset", renderer.lightmapScaleOffset);

            int kernel = combiner.FindKernel("CSMain");
            ComputeBuffer triangles = new ComputeBuffer(mesh.triangles.Length, sizeof(int));
            ComputeBuffer uv0 = new ComputeBuffer(mesh.uv.Length, sizeof(float) * 2);
            ComputeBuffer uv1 = new ComputeBuffer(mesh.uv2.Length, sizeof(float) * 2);

            RenderTexture diffuseR = new RenderTexture(tex.width, tex.height, 0);

            diffuseR.enableRandomWrite = true;
            var _rt = RenderTexture.active;

            RenderTexture.active = diffuseR;
            
            Graphics.Blit(tex, diffuseR);

            //Texture2D outer = new Texture2D(diffuseR.width, diffuseR.height);
            //outer.ReadPixels(new Rect(0, 0, outer.width, outer.height), 0, 0);
            //outer.Apply();


            RenderTexture lightmapR = new RenderTexture(lightmapTex.width, lightmapTex.height, 0);

            RenderTexture.active = lightmapR;

            Graphics.Blit(lightmapTex, lightmapR);



            triangles.SetData(mesh.triangles);
            uv0.SetData(mesh.uv);
            uv1.SetData(mesh.uv2);

            combiner.SetTexture(kernel, "baseColor", tex);
            combiner.SetTexture(kernel, "lightmap", lightmapTex);

            combiner.SetBuffer(kernel, "triangles", triangles);
            combiner.SetBuffer(kernel, "uv0", uv0);
            combiner.SetBuffer(kernel, "uv1", uv1);

            int resolution = PipelineSettings.CombinedTextureResolution;

            combiner.SetInt("width", resolution);
            combiner.SetInt("height", resolution);

            RenderTexture inTriMap = new RenderTexture(resolution, resolution, 0);
            inTriMap.enableRandomWrite = true;
            
            RenderTexture finalTex = new RenderTexture(resolution, resolution, 0);
            finalTex.enableRandomWrite = true;

            combiner.SetTexture(kernel, "Result", finalTex);
            combiner.SetTexture(kernel, "inTriMap", inTriMap);

            int threadGCount = finalTex.width * finalTex.height / 128 + 1;

            combiner.Dispatch(kernel, threadGCount, 1, 1);

            Texture2D dud = new Texture2D(resolution, resolution);

            RenderTexture.active = inTriMap;
            dud.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            dud.Apply();
            //AssetDatabase.CreateAsset(dud, "Assets/Debug/pre_pad.asset");

            kernel = combiner.FindKernel("AddPadding");
            combiner.SetBuffer(kernel, "triangles", triangles);
            combiner.SetBuffer(kernel, "uv0", uv0);
            combiner.SetBuffer(kernel, "uv1", uv1);

            combiner.SetTexture(kernel, "Result", finalTex);
            combiner.SetTexture(kernel, "inTriMap", inTriMap);

            combiner.Dispatch(kernel, threadGCount, 1, 1);

            triangles.Release();
            uv0.Release();
            uv1.Release();

            Texture2D result = new Texture2D(resolution, resolution);
            RenderTexture.active = finalTex;
            result.ReadPixels(new Rect(0, 0, result.width, result.height), 0, 0);
            result.Apply();
            //AssetDatabase.CreateAsset(result, "Assets/Debug/result.asset");
            RenderTexture.active = _rt;

            inTriMap.Release();

            return result;
        }

        private static GLTF.Schema.Material ConvertSeinPBRMaterial(UnityEngine.Material mat, ExporterEntry entry, Renderer renderer)
        {
            var material = new GLTF.Schema.Material();
            material.Name = mat.name;
            material.DoubleSided = true;
            bool hasLightmap = renderer != null && renderer.lightmapIndex >= 0;

            bool isMetal = mat.GetInt("workflow") == 0;
            bool isUnlit = mat.GetInt("unlit") == 1;
            bool doCombineBake = PipelineSettings.lightmapMode == LightmapMode.BAKE_COMBINED && hasLightmap;
            bool doDualBake = PipelineSettings.lightmapMode == LightmapMode.BAKE_SEPARATE && hasLightmap;
            if (doCombineBake)
                isUnlit = true;
            if (!isMetal)
            {
                // special
                entry.AddExtension("KHR_materials_pbrSpecularGlossiness");
                material.Extensions = new Dictionary<string, Extension>();
            }
            else
            {
                material.PbrMetallicRoughness = new PbrMetallicRoughness();
            }
            bool hasTransparency = ProcessTransparency(mat, material);

            

            if (isUnlit || isMetal)
            {
                if (mat.GetTexture("_baseColorMap") != null || doCombineBake)
                {
                    var tex = (Texture2D)mat.GetTexture("_baseColorMap");
                    

                    if (doCombineBake)
                    {
                        var lightmapParms = LightmapSettings.lightmaps[renderer.lightmapIndex];

                        var lightmapCol = lightmapParms.lightmapColor;
                        if (tex == null)
                        {
                            tex = new Texture2D(lightmapCol.width, lightmapCol.height);
                        }

                        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        tex = GenerateBakedDiffuse(tex, lightmapCol, mesh, renderer);
                    }

                    var id = entry.SaveTexture(tex, hasTransparency);
                    material.PbrMetallicRoughness.BaseColorTexture = new TextureInfo { Index = id , TexCoord = 0 };
                }

                if (mat.GetColor("_baseColor") != null)
                {
                    Color c = mat.GetColor("_baseColor");
                    material.PbrMetallicRoughness.BaseColorFactor = Utils.ExportColor(c);
                }
            }

            if (isUnlit)
            {
                if (material.Extensions == null)
                {
                    material.Extensions = new Dictionary<string, Extension>();
                }
                ExtensionManager.Serialize(ExtensionManager.GetExtensionName(typeof(KHR_materials_unlitExtensionFactory)), entry, material.Extensions);
            }
            else if (isMetal)
            {
                bool hasPBRMap = mat.GetTexture("_metallicMap") != null;
                if (hasPBRMap)
                {
                    Texture2D metallicTexture = (Texture2D)mat.GetTexture("_metallicMap");
                    Texture2D roughnessTexture = (Texture2D)mat.GetTexture("_roughnessMap");
                    Texture2D occlusion = (Texture2D)mat.GetTexture("_occlusionMap");

                    if (hasLightmap)
                    {
                        var lightmap = LightmapSettings.lightmaps[renderer.lightmapIndex];
                        occlusion = lightmap.shadowMask;
                    }

                    var metalRoughTextureAo = CreateOcclusionMetallicRoughnessTexture(
                        ref metallicTexture, ref roughnessTexture, ref occlusion
                    );
                    var assetPath = AssetDatabase.GetAssetPath(metallicTexture);
                    var ext = Path.GetExtension(assetPath);
                    var id = entry.SaveTexture(metalRoughTextureAo, hasTransparency, assetPath.Replace(ext, "-orm") + ext);
                    material.PbrMetallicRoughness.MetallicRoughnessTexture = new TextureInfo { Index = id };

                    if (occlusion != null)
                    {
                        material.OcclusionTexture = new OcclusionTextureInfo
                        {
                            Index = id,
                            Strength = 1//mat.GetFloat("_occlusionStrength")
                        };
                    }
                }

                material.PbrMetallicRoughness.MetallicFactor = mat.GetFloat("_metallic");
                material.PbrMetallicRoughness.RoughnessFactor = mat.GetFloat("_roughness");
            }
            else
            {
                TextureInfo specGlossMap = null;
                TextureInfo diffuseMap = null;
                var diffuseColor = new GLTF.Math.Color();

                if (mat.GetTexture("_baseColorMap") != null || doCombineBake)
                {
                    var tex = (Texture2D)mat.GetTexture("_baseColorMap");
                    
                    if(doCombineBake)
                    {
                        var lightmap = LightmapSettings.lightmaps[renderer.lightmapIndex];
                        if (tex == null)
                        {
                            tex = new Texture2D(PipelineSettings.CombinedTextureResolution,
                                PipelineSettings.CombinedTextureResolution) ;
                        }

                        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        var lightmapParms = LightmapSettings.lightmaps[renderer.lightmapIndex];

                        var lightmapCol = lightmapParms.lightmapColor;

                        tex = GenerateBakedDiffuse(tex, lightmapCol, mesh, renderer);
                    }
                    var id = entry.SaveTexture(tex, hasTransparency);
                    diffuseMap = new TextureInfo { Index = id, TexCoord = 0 };
                }

                if (mat.GetColor("_baseColor") != null)
                {
                    Color c = mat.GetColor("_baseColor");
                    diffuseColor = Utils.ExportColor(c);
                }

                bool hasPBRMap = mat.GetTexture("_specularGlossinessMap") != null;

                if (hasPBRMap)
                {
                    specGlossMap = new TextureInfo { Index = entry.SaveTexture((Texture2D)mat.GetTexture("_specularGlossinessMap"), true) };
                }

                var specularFactor = hasPBRMap ? Color.white : (Color)Utils.ExportColorVec4(mat.GetColor("_specular"));
                var glossinessFactor = hasPBRMap ? 1.0f : mat.GetFloat("_glossiness");

                if (material.Extensions == null)
                {
                    material.Extensions = new Dictionary<string, Extension>();
                }
                material.Extensions.Add(
                    "KHR_materials_pbrSpecularGlossiness",
                    new KHR_materials_pbrSpecularGlossinessExtension(
                        diffuseColor, diffuseMap,
                        new GLTF.Math.Vector3(specularFactor.r, specularFactor.g, specularFactor.b),
                        glossinessFactor, specGlossMap
                    )
                );

                Texture2D occlusion = (Texture2D)mat.GetTexture("_occlusionMap");
                if (occlusion != null)
                {
                    material.OcclusionTexture = new OcclusionTextureInfo
                    {
                        Index = entry.SaveTexture((Texture2D)mat.GetTexture("_occlusionMap"), false),
                        Strength = mat.GetFloat("_occlusionStrength")
                    };
                }

                
            }

            if (mat.GetTexture("_normalMap") != null)
            {
                var normMap = entry.SaveTexture((Texture2D)mat.GetTexture("_normalMap"));
                /*if(material.Extras == null)
                {
                    material.Extras = new JObject();
                }*/
                material.NormalTexture = new NormalTextureInfo
                {
                    Index = normMap
                };
                //material.Extras = (new JProperty("normalMap", normMap.Id));
                /*material.NormalTexture = new NormalTextureInfo
                {
                    Index = entry.SaveTexture((Texture2D)mat.GetTexture("_normalMap")),
                };*/
            }

            if (hasLightmap && doDualBake)
            {
                //three js doesn't support lightmap offsets or scales, so we must bake those into the mesh's uv2s
               /* var meshFilt = renderer.GetComponent<MeshFilter>();
                var oldMesh = meshFilt.sharedMesh;
                var mesh = UnityEngine.Mesh.Instantiate(oldMesh);
                var offset = renderer.lightmapScaleOffset;
                mesh.uv2 = mesh.uv2.Select((uv2) => uv2 / new Vector2(offset.x, offset.y) - new Vector2(offset.z, offset.w)).ToArray();
                meshFilt.sharedMesh = mesh;*/
               if(material.Extensions == null)
                {
                    material.Extensions = new Dictionary<string, Extension>();
                }
                ExtensionManager.Serialize(ExtensionManager.GetExtensionName(typeof(MOZ_lightmap_Factory)), entry, material.Extensions, component: renderer);
                ExtensionManager.Serialize(ExtensionManager.GetExtensionName(typeof(KHR_materials_unlitExtensionFactory)), entry, material.Extensions);
            }

            if (mat.GetTexture("_emissionMap") != null)
            {
                material.EmissiveTexture = new TextureInfo
                {
                    Index = entry.SaveTexture((Texture2D)mat.GetTexture("_emissionMap"), false),
                };
            }

            var emissive = mat.GetColor("_emission");
            if (!emissive.Equals(new Color(0, 0, 0)))
            {
                material.EmissiveFactor = Utils.ExportColor(emissive);
            }

            if (mat.GetInt("envReflection") != (int)SeinPBRShaderGUI.EnvReflection.Off || (ExporterSettings.Lighting.ambient && (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox || RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight)))
            {
                if (material.Extensions == null)
                {
                    material.Extensions = new Dictionary<string, Extension>();
                }
                //ExtensionManager.Serialize(ExtensionManager.GetExtensionName(typeof(Sein_imageBasedLightingExtensionFactory)), entry, material.Extensions, mat);
            }

            return material;
        }

        public static GLTF.Schema.Material ConvertSeinCustomMaterial(UnityEngine.Material mat, ExporterEntry entry)
        {
            if (_tempGO == null)
            {
                _tempGO = new GameObject();
            }

            var customMaterial = _tempGO.AddComponent<SeinCustomMaterial>();
            var className = mat.shader.name.Replace("Sein/", "");
            if (!className.Contains("Material"))
            {
                className += "Material";
            }

            var shaderPath = AssetDatabase.GetAssetPath(mat.shader);
            if (shaderPath != null)
            {
                var matScriptPath = Path.Combine(
                    shaderPath.Replace(Path.GetFileName(shaderPath), ""),
                    className + ".js"
                );

                if (File.Exists(matScriptPath))
                {
                    if (entry.root.Extensions == null)
                    {
                        entry.root.Extensions = new Dictionary<string, Extension>();
                    }

                    customMaterial.matScriptPath = matScriptPath;
                }
            }

            customMaterial.className = className;
            customMaterial.renderOrder = mat.renderQueue;
            customMaterial.unityMaterialName = mat.name;

            var floatArray = new List<SeinMaterialUniformFloat>();
            var vector4Array = new List<SeinMaterialUniformFloatVec4>();
            var colorArray = new List<SeinMaterialUniformColor>();
            var textureArray = new List<SeinMaterialUniformTexture>();

            for (int i = 0; i < ShaderUtil.GetPropertyCount(mat.shader); i += 1)
            {
                var propType = ShaderUtil.GetPropertyType(mat.shader, i);
                var propName = ShaderUtil.GetPropertyName(mat.shader, i);

                if (propName == "cloneForInst")
                {
                    customMaterial.cloneForInst = mat.GetInt("cloneForInst") != 0;
                    continue;
                }

                if (ShaderUtil.IsShaderPropertyHidden(mat.shader, i))
                {
                    continue;
                }

                var n = propName;

                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        floatArray.Add(new SeinMaterialUniformFloat { name = propName, value = mat.GetFloat(n) });
                        break;
                    case ShaderUtil.ShaderPropertyType.Color:
                        colorArray.Add(new SeinMaterialUniformColor { name = propName, value = mat.GetColor(n) });
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        vector4Array.Add(new SeinMaterialUniformFloatVec4 { name = propName, value = mat.GetVector(n) });
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        if (mat.GetTexture(n) != null)
                        {
                            textureArray.Add(new SeinMaterialUniformTexture { name = propName, value = (Texture2D)mat.GetTexture(n) });
                        }
                        break;
                }

                customMaterial.uniformsFloat = floatArray.ToArray();
                customMaterial.uniformsFloatVec4 = vector4Array.ToArray();
                customMaterial.uniformsColor = colorArray.ToArray();
                customMaterial.uniformsTexture = textureArray.ToArray();
            }

            var tempM = new GLTF.Schema.Material();
            customMaterial.transparent = ProcessTransparency(mat, tempM);

            var m = ConvertMaterial(customMaterial, entry);

            return m;
        }

        //private static GLTF.Schema.Material ConvertKHRWebGLMaterial(UnityEngine.Material material, ExportorEntry entry)
        //{

        //}

        public static GLTF.Schema.Material ConvertMaterial(SeinCustomMaterial mat, ExporterEntry entry)
        {
            var material = new GLTF.Schema.Material();
            material.Name = mat.unityMaterialName;

            if (mat.uniformsTexture.Length != 0)
            {
                foreach (var uniform in mat.uniformsTexture)
                {
                    uniform.id = entry.SaveTexture(uniform.value, mat.transparent);
                }
            }

            if (mat.uniformsCubeTexture.Length != 0)
            {
                foreach (var uniform in mat.uniformsCubeTexture)
                {
                    uniform.id = entry.SaveCubeTexture(uniform.value);
                }
            }

            if (material.Extensions == null)
            {
                material.Extensions = new Dictionary<string, Extension>();
            }

            if (mat.transparent)
            {
                material.AlphaMode = AlphaMode.BLEND;
            }

            //ExtensionManager.Serialize(ExtensionManager.GetExtensionName(typeof(Sein_customMaterialExtensionFactory)), entry, material.Extensions, mat);
            return material;
        }

        public static void SetTextureImporterFormat(Texture2D texture, bool isReadable)
        {
            if (null == texture) return;
            if (texture.isReadable) return;
            string assetPath = AssetDatabase.GetAssetPath(texture);
            var tImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (tImporter != null)
            {
                tImporter.textureType = TextureImporterType.Default;
                tImporter.isReadable = isReadable;

                AssetDatabase.ImportAsset(assetPath);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>{ exportPath,  pathInGltfFile }</returns>
        public static string[] GetAssetOutPath(UnityEngine.Object assetObject, string format = null)
        {
            string assetPath = AssetDatabase.GetAssetPath(assetObject);
            if(assetPath == null || assetPath.Trim() == "")
            {
                assetPath = Path.Combine(PipelineSettings.PipelineFolder, assetObject.GetInstanceID() + "_" + System.DateTime.Now.ToBinary().ToString() + ".tga");
                var tex = (Texture2D)assetObject;
                if(tex != null)
                {

                    File.WriteAllBytes(assetPath, tex.EncodeToTGA());
                }

            }
            return GetAssetOutPath(assetPath, format);
        }

        public static string[] GetAssetOutPath(string assetPath, string format = null)
        {
            //Debug.Log("apath: " + assetPath);
            string pathInArchive = ExporterUtils.CleanPath(Path.GetDirectoryName(assetPath).Replace("Assets/Resources/", "").Replace("Assets/", ""));
            string exportDir = Path.Combine(ExporterSettings.Export.folder, pathInArchive);

            if (!Directory.Exists(exportDir))
            {
                Directory.CreateDirectory(exportDir);
            }

            string outputFilename = "";
            if (format == null)
            {
                outputFilename = Path.GetFileName(assetPath);
            }
            else
            {
                outputFilename = Path.GetFileNameWithoutExtension(assetPath) + format;
            }

            outputFilename = ExporterUtils.CleanPath(outputFilename);

            string exportPath = exportDir + "/" + outputFilename;
            string pathInGltfFile = pathInArchive + "/" + outputFilename;

            return new string[] { exportPath, pathInGltfFile };
        }

        public static void FinishExport()
        {
            if (_tempGO != null)
            {
                UnityEngine.Object.DestroyImmediate(_tempGO);
            }
        }

        private static bool ProcessTransparency(UnityEngine.Material mat, GLTF.Schema.Material material)
        {
            if (!mat.HasProperty("_Mode"))
            {
                return false;
            }

            switch ((int)mat.GetFloat("_Mode"))
            {
                // Opaque
                case 0:
                    material.AlphaMode = AlphaMode.OPAQUE;
                    return false;
                // Cutout
                case 1:
                    material.AlphaMode = AlphaMode.MASK;
                    material.AlphaCutoff = mat.GetFloat("_Cutoff");
                    break;
                // Transparent
                case 2:
                case 3:
                    material.AlphaMode = AlphaMode.BLEND;
                    break;
            }

            return true;
        }

        public static void DoActionForTexture(ref Texture2D tex, Action<Texture2D> action)
        {
            Utils.DoActionForTexture(ref tex, action);
        }

        private static Texture2D CreateOcclusionMetallicRoughnessTexture(
            ref Texture2D metallic,
            ref Texture2D roughness,
            ref Texture2D occlusion
        )
        {
            string texName = metallic.name + "_orm";
            var textureM = metallic;
            var textureR = roughness;
            var textureAO = occlusion;

            string id = "";
            if (metallic)
            {
                id += metallic.GetInstanceID();
            }
            if (roughness)
            {
                id += roughness.GetInstanceID();
            }
            if (occlusion)
            {
                id += occlusion.GetInstanceID();
            }

            if (ExporterEntry.composedTextures.ContainsKey(id))
            {
                return ExporterEntry.composedTextures[id];
            }

            var newTex = GLTFTextureUtils.packOcclusionMetalRough(metallic, roughness, occlusion, texName);
            newTex.filterMode = metallic.filterMode;
            newTex.wrapMode = metallic.wrapMode;

            ExporterEntry.composedTextures.Add(id, newTex);

            return newTex;
        }
    }
}
