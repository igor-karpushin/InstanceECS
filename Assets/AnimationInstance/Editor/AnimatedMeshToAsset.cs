#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimationInstance.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AnimationInstance.Editor
{
    public class AnimatedMeshToAsset
    {

        class AnimInfo
        {
            public int Start;
            public int Count;
        }
        
        private const int BoneMatrixRowCount = 3;
        private const int TargetFrameRate = 30;

        [MenuItem("AnimatedMeshRendererGenerator/MeshToAsset")]
        private static void Generate()
        {
            var targetObject = Selection.activeGameObject;
            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("Warning", "Selected object type is not gameobject.", "OK");
                return;
            }

            var skinnedMeshRenderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (!skinnedMeshRenderers.Any() || skinnedMeshRenderers.Count() != 1)
            {
                EditorUtility.DisplayDialog("Warning", "Selected object does not have one skinnedMeshRenderer.", "OK");
                return;
            }

            var animator = targetObject.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("Warning", "Selected object does not have Animator.", "OK");
                return;
            }

            var selectionPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(targetObject));
            var skinnedMeshRenderer = skinnedMeshRenderers.First();
            var clips = animator.runtimeAnimatorController.animationClips;
            
            Directory.CreateDirectory(Path.Combine(selectionPath, "AnimatedMesh"));
            
            var animationTexture = GenerateAnimationTexture(targetObject, clips, skinnedMeshRenderer);
            AssetDatabase.CreateAsset(animationTexture,
                $"{selectionPath}/AnimatedMesh/{targetObject.name}_AnimationTexture.asset");
            
            var mesh = GenerateUvBoneWeightedMesh(skinnedMeshRenderer);
            AssetDatabase.CreateAsset(mesh, $"{selectionPath}/AnimatedMesh/{targetObject.name}_Mesh.asset");
            
            var materials = GenerateMaterial(skinnedMeshRenderer, animationTexture, skinnedMeshRenderer.bones.Length);
            foreach (var material in materials)
            {
                AssetDatabase.CreateAsset(material, $"{selectionPath}/AnimatedMesh/{material.name}.asset");
            }
            GenerateMeshRendererObject(targetObject, mesh, materials, out var go);
            PrefabUtility.CreatePrefab($"{selectionPath}/AnimatedMesh/{targetObject.name}.prefab", go);

            Object.DestroyImmediate(go);
        }

        private static Mesh GenerateUvBoneWeightedMesh(SkinnedMeshRenderer smr)
        {
            var mesh = Object.Instantiate(smr.sharedMesh);

            var boneSets = smr.sharedMesh.boneWeights;
            var boneIndexes = boneSets.Select(x => new Vector4(x.boneIndex0, x.boneIndex1, x.boneIndex2, x.boneIndex3)).ToList();
            var boneWeights = boneSets.Select(x => new Vector4(x.weight0, x.weight1, x.weight2, x.weight3)).ToList();

            mesh.SetUVs(2, boneIndexes);
            mesh.SetUVs(3, boneWeights);

            return mesh;
        }

        private static Texture GenerateAnimationTexture(
            GameObject targetObject, 
            IEnumerable<AnimationClip> clips, 
            SkinnedMeshRenderer smr)
        {
            var animationClips = clips as AnimationClip[] ?? clips.ToArray();
            var textureBoundary = GetCalculatedTextureBoundary(animationClips, smr.bones.Count());

            var texture = new Texture2D(
                (int)textureBoundary.x, 
                (int)textureBoundary.y, 
                TextureFormat.RGBAHalf, 
                false, true);
            
            var pixels = texture.GetPixels();
            var pixelIndex = 0;
            
            // Setup anim
            var currentClipFrames = 0;
            foreach (var clip in animationClips)
            {
                var frameCount = (int)(clip.length * TargetFrameRate);
                var startFrame = currentClipFrames + 1;
                var endFrame = startFrame + frameCount - 1;
                try
                {
                    var animationType = (byte) Enum.Parse(typeof(AnimationType), clip.name);
                    pixels[animationType] = new Color(startFrame, frameCount, 0, 0);
                }
                catch (Exception error)
                {
                    UnityEngine.Debug.LogWarning($"Conversion Error: {error.Message}");
                }
                currentClipFrames = endFrame;
            }

            pixelIndex = 255;

            //Setup 0 to bindPoses
            foreach (var boneMatrix in smr.bones.Select((b, idx) => b.localToWorldMatrix * smr.sharedMesh.bindposes[idx]))
            {
                pixels[pixelIndex++] = new Color(boneMatrix.m00, boneMatrix.m01, boneMatrix.m02, boneMatrix.m03);
                pixels[pixelIndex++] = new Color(boneMatrix.m10, boneMatrix.m11, boneMatrix.m12, boneMatrix.m13);
                pixels[pixelIndex++] = new Color(boneMatrix.m20, boneMatrix.m21, boneMatrix.m22, boneMatrix.m23);
            }

            foreach (var clip in animationClips)
            {
                var totalFrames = (int)(clip.length * TargetFrameRate);
                foreach (var frame in Enumerable.Range(0, totalFrames))
                {
                    clip.SampleAnimation(targetObject, (float)frame / TargetFrameRate);

                    foreach (var boneMatrix in smr.bones.Select((b, idx) => b.localToWorldMatrix * smr.sharedMesh.bindposes[idx]))
                    {
                        pixels[pixelIndex++] = new Color(boneMatrix.m00, boneMatrix.m01, boneMatrix.m02, boneMatrix.m03);
                        pixels[pixelIndex++] = new Color(boneMatrix.m10, boneMatrix.m11, boneMatrix.m12, boneMatrix.m13);
                        pixels[pixelIndex++] = new Color(boneMatrix.m20, boneMatrix.m21, boneMatrix.m22, boneMatrix.m23);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            return texture;
        }

        private static Vector2 GetCalculatedTextureBoundary(IEnumerable<AnimationClip> clips, int boneLength)
        {
            var boneMatrixCount = BoneMatrixRowCount * boneLength;

            var totalPixels = clips.Aggregate(
                boneMatrixCount, (pixels, currentClip) => 
                    pixels + boneMatrixCount * (int)(currentClip.length * TargetFrameRate));

            var textureWidth = 1;
            var textureHeight = 1;

            while (textureWidth * textureHeight < totalPixels)
            {
                if (textureWidth <= textureHeight)
                {
                    textureWidth *= 2;
                }
                else
                {
                    textureHeight *= 2;
                }
            }

            return new Vector2(textureWidth, textureHeight);
        }

        private static Material[] GenerateMaterial(Renderer smr, Texture texture, int boneLength)
        {
            var materials = new List<Material>();
            foreach (var sharedMaterial in smr.sharedMaterials)
            {
                var material = Object.Instantiate(sharedMaterial);
                material.name = $"{smr.gameObject.name}Material{materials.Count}";
                material.shader = Shader.Find("Shader Graphs/InstanceShader");
                material.SetTexture("_AnimTex", texture);
                material.SetInt("_PixelCountPerFrame", BoneMatrixRowCount * boneLength);
                material.enableInstancing = true;
                materials.Add(material);
            }
            return materials.ToArray();
        }

        private static void GenerateMeshRendererObject(
            GameObject targetObject, 
            Mesh mesh, 
            Material[] materials,
            out GameObject instancePrefab)
        {
            instancePrefab = new GameObject {name = targetObject.name};

            var mf = instancePrefab.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = instancePrefab.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.lightProbeUsage = LightProbeUsage.Off;
        }
    }
}
#endif