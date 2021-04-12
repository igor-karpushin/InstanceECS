using System.Collections.Generic;
using AnimationInstance.Scripts;
using UnityEditor;
using UnityEngine;

namespace AnimationInstance.Editor
{
    
    public class InstanceShaderGui : ShaderGUI 
    {
        public override void OnGUI (MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var refactor = new List<MaterialProperty>();
            MaterialProperty animationType = null;
            foreach (var property in properties)
            {
                if (property.name == "_AnimationType")
                {
                    animationType = property;
                }
                else
                {
                    refactor.Add(property);
                }
            }

            properties = refactor.ToArray();
            base.OnGUI (materialEditor, properties);

            var type = (AnimationType) animationType.floatValue;
            animationType.floatValue = (float)((AnimationType)EditorGUILayout.EnumPopup("Animation:", type));
            
            var targetMaterial = materialEditor.target as Material;
            targetMaterial.SetFloat(animationType.name, animationType.floatValue);
        }
    }
}
