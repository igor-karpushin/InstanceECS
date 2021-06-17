using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimationInstance.Ecs;
using AnimationInstance.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AnimationInstance.Editor
{
    public class AnimationGenerator : EditorWindow
    {

        class ClipInstance
        {
            public bool Enable;
            public int Start;
            public int Count;
            public int Fps;
            public AnimationClip Source;
        }

        class PrefabInstance
        {
            public GameObject Source;
            public bool Extend;
            public Dictionary<string, ClipInstance> Clips;
        }
        
        static AnimationGenerator s_Window;
        const byte k_AnimHeader = 255; 

        List<PrefabInstance> m_Prefabs;
        Texture2D m_AnimationTexture;
        Dictionary<int, int> m_PartialTextureIndex;
        Rect[] m_TexturePackRects;
        Texture2D m_BaseTexture;
        
        //static readonly int s_PixelCountPerFrame = Shader.PropertyToID("_PixelCountPerFrame");
        static readonly int s_PixelStart = Shader.PropertyToID("_PixelStart");
        static readonly int s_AnimTex = Shader.PropertyToID("_AnimTex");
        static readonly int s_MainTex = Shader.PropertyToID("_MainTex");

        void OnEnable()
        {
            m_Prefabs = new List<PrefabInstance>();
            EditorApplication.update += GenerateAnimation;
        }
        
        void OnDisable()
        {
            EditorApplication.update -= GenerateAnimation;
            m_BaseTexture = null;
            m_TexturePackRects = null;
        }
        
        void GenerateAnimation()
        {
            
        }

        
        private void Reset()
        {
          
        }
        
        [MenuItem("chamele0n/Animation Generator", false)]
        static void MakeWindow()
        {
            s_Window = GetWindow(typeof(AnimationGenerator)) as AnimationGenerator;
        }

        void OnGUI()
        {
            GUI.skin.label.richText = true;
            GUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical();
            for(var i = 0; i < m_Prefabs.Count; ++i)
            {
                EditorGUILayout.BeginHorizontal();
                
                var prefabInstance = m_Prefabs[i];
                var windowElement = (GameObject)EditorGUILayout.ObjectField(prefabInstance.Source, typeof(GameObject), true);
                if (windowElement)
                {
                    if (prefabInstance.Source == null || windowElement != prefabInstance.Source)
                    {
                        var renderers = windowElement.GetComponentsInChildren<SkinnedMeshRenderer>();
                        if (renderers.Length > 0)
                        {
                            var animator = windowElement.GetComponentInChildren<Animator>();
                            if (animator != null)
                            {
                                prefabInstance.Source = windowElement;
                                prefabInstance.Extend = true;
                                var clips = animator.runtimeAnimatorController.animationClips;
                                foreach (var clip in clips)
                                {
                                    prefabInstance.Clips.Add(clip.name, new ClipInstance
                                    {
                                        Enable = false,
                                        Fps = 15,
                                        Source = clip
                                    });
                                }
                            }
                            else
                            {
                                EditorUtility.DisplayDialog("Warning", "Selected object does not have Animator.", "OK");
                            }
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Warning", $"{windowElement.name} >> SkinnedMeshRenderer: {renderers.Length}", "OK");
                        }
                    }
                }
                
                GUI.enabled = prefabInstance.Source != null;
                var buttonStyle = new GUIStyle(GUI.skin.button);
                prefabInstance.Extend = GUILayout.Toggle(prefabInstance.Extend, "Clips", buttonStyle);
                GUI.enabled = true;
                
                if (GUILayout.Button("remove"))
                {
                    m_Prefabs.RemoveAt(i);
                }
                
                EditorGUILayout.EndHorizontal();

                if (prefabInstance.Extend)
                {
                    foreach (var clipName in prefabInstance.Clips.Keys)
                    {
                        var animInfo = prefabInstance.Clips[clipName];
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        animInfo.Enable = GUILayout.Toggle(animInfo.Enable, $"{clipName}", new GUIStyle(GUI.skin.toggle)
                        {
                            alignment = TextAnchor.MiddleRight,
                            fixedWidth = 100
                        });
                        
                        GUI.enabled = animInfo.Enable;
                        GUILayout.Space(20);
                        animInfo.Fps = (int)GUILayout.HorizontalSlider(animInfo.Fps, 15, 60);
                        GUILayout.Label(animInfo.Fps.ToString());
                        
                        GUILayout.Space(40);
                        
                        GUI.enabled = true;
                        
                        EditorGUILayout.EndHorizontal();
                    }
                }

            }
            
            if (GUILayout.Button("Add Prefab"))
            {
                m_Prefabs.Add(new PrefabInstance
                {
                    Clips = new Dictionary<string, ClipInstance>(),
                    Extend = false
                });
            }
            
            if (m_AnimationTexture != null)
            {
                GUILayout.Label($"Texture Size: {m_AnimationTexture.width}x{m_AnimationTexture.height}", new GUIStyle
                {
                    fixedWidth = 20, 
                    padding = new RectOffset(5, 0, 5, 0), 
                    normal = { textColor = Color.white }
                });
                var lastRect = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawPreviewTexture(new Rect(5f, lastRect.y + lastRect.height + 5, m_AnimationTexture.width, m_AnimationTexture.height), m_AnimationTexture);
                GUILayout.Space(m_AnimationTexture.height + 5);
            }
            
            if (GUILayout.Button("Generate"))
            {
                // directory
                BuildAnimationTexture();
            }

            EditorGUILayout.EndVertical();
        }

        void ForceDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        Mesh BuildMesh(SkinnedMeshRenderer render)
        {
            var mesh = Instantiate(render.sharedMesh);
            var uvUpdate = new Vector2[mesh.uv.Length];
            for (var i = 0; i < mesh.subMeshCount; ++i)
            {
                var mainTexture = render.sharedMaterials[i].mainTexture;
                
                var textureHash = mainTexture.GetHashCode();
                var rectIndex = m_PartialTextureIndex[textureHash];
                var textureRect = m_TexturePackRects[rectIndex];

                var subMeshInfo = mesh.GetSubMesh(i);
                for (var v = 0; v < subMeshInfo.vertexCount; ++v)
                {
                    var uvVector = mesh.uv[subMeshInfo.firstVertex + v];
                    uvUpdate[subMeshInfo.firstVertex + v] = new Vector2
                    {
                        x = uvVector.x * textureRect.width + textureRect.x,
                        y = uvVector.y * textureRect.height + textureRect.y
                    };
                }
            }
            
            var boneSets = render.sharedMesh.boneWeights;
            var boneIndexes = boneSets.Select(x => new Vector4(x.boneIndex0, x.boneIndex1, x.boneIndex2, x.boneIndex3)).ToList();
            var boneWeights = boneSets.Select(x => new Vector4(x.weight0, x.weight1, x.weight2, x.weight3)).ToList();

            mesh.SetUVs(0, uvUpdate);
            mesh.SetUVs(2, boneIndexes);
            mesh.SetUVs(3, boneWeights);
            return mesh;
        }
        
        GameObject GenerateMeshRendererObject(string prefabName, Mesh mesh, Material material)
        {
            var instancePrefab = new GameObject {name = prefabName};
            var subMeshCount = mesh.subMeshCount;

            var mf = instancePrefab.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var renderer = instancePrefab.AddComponent<MeshRenderer>();
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            
            var sharedMaterials = new Material[subMeshCount];
            for(var i = 0; i < subMeshCount; ++i)
            {
                sharedMaterials[i] = material;
            }

            renderer.sharedMaterials = sharedMaterials;
            return instancePrefab;
        }
        
        void PrepareTexture(Object texture)
        {
            var assetPath = AssetDatabase.GetAssetPath( texture );
            var tImporter = AssetImporter.GetAtPath( assetPath ) as TextureImporter;
            if (tImporter != null)
            {
                if (!tImporter.isReadable)
                {
                    tImporter.isReadable = true;
                    AssetDatabase.ImportAsset(assetPath);
                }
            }
        }

        void BuildAnimationTexture()
        {
            var pixelIndex = 0;
            var writePixels = new Color[2048 * 2048];
            var basePath = Path.Combine("Assets", "AnimationModels");
            ForceDirectory(basePath);

            m_BaseTexture = new Texture2D(4096, 4096);
            AssetDatabase.CreateAsset(m_BaseTexture, Path.Combine(basePath, "BaseTexture.asset"));
            
            var baseMaterial = new Material(Shader.Find("Shader Graphs/InstanceShader")) { name = "BaseMaterial", enableInstancing = true };
            AssetDatabase.CreateAsset(baseMaterial, Path.Combine(basePath, "BaseMaterial.mat"));
            
            var partialTextures = new Dictionary<int, Texture2D>();
            m_PartialTextureIndex = new Dictionary<int, int>();
    
            // create texture atlas
            foreach (var prefabInstance in m_Prefabs)
            {
                var renderer = prefabInstance.Source.GetComponentInChildren<SkinnedMeshRenderer>();
                var sharedMesh = renderer.sharedMesh;
                for (var i = 0; i < sharedMesh.subMeshCount; ++i)
                {
                    // textures
                    var mainTexture = renderer.sharedMaterials[i].mainTexture;
                    var textureHash = mainTexture.GetHashCode();
                    if (!partialTextures.ContainsKey(textureHash))
                    {
                        PrepareTexture(mainTexture);
                        m_PartialTextureIndex.Add(textureHash, partialTextures.Count);
                        partialTextures.Add(textureHash, mainTexture as Texture2D);
                    }
                }
            }

            m_TexturePackRects = m_BaseTexture.PackTextures(
                partialTextures.Values.ToArray(), 0, 4096);
            
            foreach (var prefabInstance in m_Prefabs)
            {
                var prefabDirectory = Path.Combine(basePath, prefabInstance.Source.name);
                ForceDirectory(prefabDirectory);
                
                var renderer = prefabInstance.Source.GetComponentInChildren<SkinnedMeshRenderer>();
                var startPixels = pixelIndex;
                pixelIndex += k_AnimHeader;

                // create mesh
                var prefabMesh = BuildMesh(renderer);
                AssetDatabase.CreateAsset(prefabMesh, Path.Combine(prefabDirectory, "Mesh.asset"));

                var clonePrefab = GenerateMeshRendererObject(prefabInstance.Source.name, prefabMesh, baseMaterial);
                var configComponent = clonePrefab.AddComponent<AnimationConvertComponent>();
                configComponent.PixelCount = renderer.bones.Length * 3;
                configComponent.PixelStart = startPixels;
                
                PrefabUtility.SaveAsPrefabAsset(clonePrefab, Path.Combine(prefabDirectory, $"{clonePrefab.name}.prefab"));
                
                foreach (var boneMatrix in renderer.bones.Select((b, idx) => b.localToWorldMatrix * renderer.sharedMesh.bindposes[idx]))
                {
                    writePixels[pixelIndex++] = new Color(boneMatrix.m00, boneMatrix.m01, boneMatrix.m02, boneMatrix.m03);
                    writePixels[pixelIndex++] = new Color(boneMatrix.m10, boneMatrix.m11, boneMatrix.m12, boneMatrix.m13);
                    writePixels[pixelIndex++] = new Color(boneMatrix.m20, boneMatrix.m21, boneMatrix.m22, boneMatrix.m23);
                }
                
                var currentClipFrames = 0;
                foreach (var clipName in prefabInstance.Clips.Keys)
                {
                    var clipInstance = prefabInstance.Clips[clipName];
                    if (clipInstance.Enable)
                    {
                        var frameCount = (int)(clipInstance.Source.length * clipInstance.Fps);
                        var startFrame = currentClipFrames + 1;
                        
                        // write animation info
                        try
                        {
                            var animationType = (byte)Enum.Parse(typeof(AnimationType), clipName);
                            writePixels[startPixels + animationType] = new Color(startFrame, frameCount, clipInstance.Fps, 0);
                        }
                        catch (Exception error)
                        {
                            Debug.LogWarning($"Conversion Error: {error.Message}");
                        }

                        currentClipFrames = startFrame + frameCount - 1;

                        // write frames
                        foreach (var frame in Enumerable.Range(0, frameCount))
                        {
                            clipInstance.Source.SampleAnimation(prefabInstance.Source, (float)frame / clipInstance.Fps);

                            foreach (var boneMatrix in renderer.bones.Select((b, idx) => b.localToWorldMatrix * renderer.sharedMesh.bindposes[idx]))
                            {
                                writePixels[pixelIndex++] = new Color(boneMatrix.m00, boneMatrix.m01, boneMatrix.m02, boneMatrix.m03);
                                writePixels[pixelIndex++] = new Color(boneMatrix.m10, boneMatrix.m11, boneMatrix.m12, boneMatrix.m13);
                                writePixels[pixelIndex++] = new Color(boneMatrix.m20, boneMatrix.m21, boneMatrix.m22, boneMatrix.m23);
                            }
                        }
                    }
                }
                
                //DestroyImmediate(clonePrefab);
            }
            
            if (pixelIndex == 0)
            {
                m_AnimationTexture = null;
                return;
            }

            var textureWidth = 1;
            var textureHeight = 1;

            while (textureWidth * textureHeight < pixelIndex)
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
            
            var texturePixels = new Color[textureWidth * textureHeight];
            Array.Copy(writePixels, texturePixels, pixelIndex);
            
            m_AnimationTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBAHalf,false, true);
            m_AnimationTexture.SetPixels(texturePixels);
            m_AnimationTexture.Apply();
            m_AnimationTexture.filterMode = FilterMode.Point;

            AssetDatabase.CreateAsset(m_AnimationTexture, Path.Combine(basePath, "AnimationTexture.asset"));
            baseMaterial.SetTexture(s_AnimTex, m_AnimationTexture);
            baseMaterial.SetTexture(s_MainTex, m_BaseTexture);
            baseMaterial.SetInt(s_PixelStart, pixelIndex);
        }
        
    }
}